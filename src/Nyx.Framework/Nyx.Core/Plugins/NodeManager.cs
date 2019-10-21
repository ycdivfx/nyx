/*
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 * The Original Code is Copyright (C) 2016 You can do it! VFX & JDBGraphics
 * All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Nyx.Core.Extensions;
using Nyx.Core.Logging;
using Nyx.Core.Management;
using Nyx.Core.Messaging;
using Nyx.Core.Reflection;
using Nyx.Core.Threading;
using Nyx.Core.Utils;

namespace Nyx.Core.Plugins
{
    /// <summary>
    ///     Node Manager for the mindless hub. This is implemented in the core so its "free" for every borg/hub system.
    /// </summary>
    [Extension(nameof(NodeManager), ExtensionLifeCycle.Shared)]
    public sealed class NodeManager : INyxMessageFilter, IHubAction, IBorgAction, INyxService, IExtendNodeInfo, IDisposable
    {
        private const string NodesInfo = "core.nodes.info";
        private const string NodesInfoReport = "core.nodes.info.report";
        private const string NodesUpdateSubscribe = "core.nodes.update.subscribe";
        private const string NodesPing = "core.nodes.ping";
        public const string NodesPingStop = "core.nodes.ping.stop";
        private const string NodeManagerAll = "core.nodes.param.all";
        private const string NodeManagerAdded = "core.nodes.param.added";
        private const string NodeManagerRemoved = "core.nodes.param.removed";
        private const string NodeManagerCleanup = "core.nodes.param.cleanup";
        private const string NodeManagerInfo = "node_info";
        public const string NodeManagerNodes = "nodemanager.nodes";
        public const string NodeManagerSubscribeUpdates = "nodemanager.subscribeupdates";
        private volatile bool _stop;
        private readonly INyxBorg _borg;
        private readonly GroupsInfo _groups = new GroupsInfo();
        private readonly INyxHub _hub;
        private readonly ILogger<NodeManager> _logger;
        private readonly PluginManager _pluginManager;
        private readonly ISubject<GroupsInfo> _groupSubject = new BehaviorSubject<GroupsInfo>(new GroupsInfo());
        private readonly ISubject<INyxMessage> _messageReceivedBorg;
        private readonly ISubject<INyxMessage> _messageReceivedHub;
        private readonly ISubject<NodesChanges> _nodesSubject = new Subject<NodesChanges>();
        private readonly List<NodeInfo> _nodesInfo = new List<NodeInfo>();
        private readonly HashSet<string> _pushUpdates = new HashSet<string>();
        private readonly ComputerInfo _computerInfo;
        private bool _connectedToHub = false;
        private bool _subscribed;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly MultipleAssignmentDisposable _subscriptionDisposable = new MultipleAssignmentDisposable();
        private readonly SerialAsyncTasker _tasker;
        private static readonly object ProcessLock = new object();
        private static readonly object FilterLock = new object();
        private static readonly object NodesLock = new object();
        private static readonly object UpdaterLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly IObservable<bool> _internalHubWatch;

        /// <summary>
        ///     Singleton access to the NodeManager.
        /// </summary>
        public static NodeManager Current { get; private set; }

        /// <summary>
        ///     Observable for replacing the MessageBus.
        /// </summary>
        public IObservable<NodesChanges> WhenNodesChange => _nodesSubject.AsObservable();

        /// <summary>
        ///     Observable for Group changes.
        /// </summary>
        public IObservable<GroupsInfo> WhenGroupsChange => _groupSubject.AsObservable();

        /// <summary>
        ///     Read only access to the node list.
        /// </summary>
        public IReadOnlyList<NodeInfo> Nodes => new List<NodeInfo>(_nodesInfo);

        /// <summary>
        ///     Supported actions vary with the host node.
        /// </summary>
        public IEnumerable<string> SupportedActions { get; }

        /// <summary>
        ///     Processes all kinds of messages
        /// </summary>
        public MessageDirection Direction => MessageDirection.In | MessageDirection.Out;

        /// <summary>
        ///     Gets all the groups available
        /// </summary>
        public GroupsInfo Groups => _groups.Clone();

        public NodeManager(IEnumerable<INyxNode> runningNodes,
            ILogger<NodeManager> logger,
            PluginManager pluginManager)
        {
            Current = this;
            _logger = logger;
            _pluginManager = pluginManager;
            _tasker = new SerialAsyncTasker();
            _connectedToHub = true;
            // Find the hub if its running in this app domain.
            var nyxNodes = runningNodes as IList<INyxNode> ?? runningNodes.ToList();
            _hub = nyxNodes.FirstOrDefault(n => n is INyxHub) as INyxHub;
            _borg = nyxNodes.FirstOrDefault(n => n is INyxBorg) as INyxBorg;
            if (_borg != null) _computerInfo = new ComputerInfo();
            _messageReceivedBorg = new Subject<INyxMessage>();
            _messageReceivedHub = new Subject<INyxMessage>();
            SupportedActions = _hub != null
                ? new[] {NodesInfo, NodesUpdateSubscribe, NodesPing, NodesPingStop}
                : new[] {NodesInfoReport};
                // Run the cleaner in a queue with a max of 5 items in the queue.
            if (_borg != null)
            {
                _internalHubWatch = Observable.Create<bool>(o =>
                {
                    var disposable = new CompositeDisposable();
                    var synlock = new object();
                    var hubOnline = false;
                    disposable.Add(_borg.ConnectionStatusStream
                        .DistinctUntilChanged()
                        .Subscribe(c =>
                        {
                            lock (synlock)
                            {
                                var tempState = c.HasFlag(ConnectionStatus.Online);
                                if(tempState == hubOnline) return;
                                hubOnline = tempState;
                                o.OnNext(tempState);
                            }
                        }));
                    return disposable;
                }).Publish().RefCount();

                _disposables.Add(_internalHubWatch
                    .Where(s => !s)
                    .ObserveOnPool()
                    .Subscribe(o =>
                    {
                        lock (NodesLock)
                        {
                            var removed = new List<NodeInfo>(_nodesInfo);
                            _nodesInfo.Clear();
                            _groups.NodesByGroup.Clear();
                            _nodesSubject.OnNext(new NodesChanges {Removed = removed});
                            _groupSubject.OnNext(GroupsInfo.Empty);
                            _logger.Debug("Removed all nodes. Hub is offline or we lost connection.");
                        }
                    }));
            }
            if (_hub == null) return;
            _disposables.Add(Observable.Interval(TimeSpan.FromMinutes(1)).ObserveOn(new OrderedTaskScheduler(5)).Subscribe(o =>
            {
                lock (NodesLock)
                {
                    var removed = new List<NodeInfo>();
                    _nodesInfo.RemoveAll(ni =>
                    {
                        var res = DateTime.Now.Subtract(ni.TimeStamp).TotalSeconds > 140;
                        if (res) removed.Add(ni);
                        return res;
                    });
                    if (removed.Count == 0) return;
                    _logger.Debug("Removed {0} dead nodes.", removed.Count);
                    foreach (var update in _pushUpdates)
                        SendNodesInfo(null, update, new List<NodeInfo>(), removed);
                }
            }));
        }

        #region Borg/Hub Action
        /// <summary>
        ///     Process messages on borg or host conform the case.
        /// </summary>
        /// <param name="message">Message to process.</param>
        /// <returns></returns>
        public bool ProcessMessage(INyxMessage message)
        {
            // We lock here so we can process this without caring too much.
            lock (ProcessLock)
            {
                if (_hub != null)
                {
                    _messageReceivedHub.OnNext(message);
                    switch (message.Action)
                    {
                        case NodesInfo:
                            SendNodesInfo(message, new List<NodeInfo>(_nodesInfo), new List<NodeInfo>(), true);
                            return true;
                        case NodesUpdateSubscribe:
                            SendNodesInfo(message, new List<NodeInfo>(_nodesInfo), new List<NodeInfo>(), true);
                            lock(UpdaterLock)
                            {
                                if (!_pushUpdates.Contains(message.Source))
                                _pushUpdates.Add(message.Source);
                            }
                            break;
                        case NodesPing:
                            // This is just so we update the info of the node, no action required.
                            break;
                    }
                    return true;
                }
                if (_borg == null) return true;
                switch (message.Action)
                {
                    case NodesInfoReport:
                        BuildReportFromMessage(message);
                        break;
                }
                _messageReceivedBorg.OnNext(message);
            }
            return true;
        }

        private void BuildReportFromMessage(INyxMessage message)
        {
            lock (NodesLock)
            {
                var added = message.Get<IEnumerable<NodeInfo>>(NodeManagerAdded);
                var removed = message.Get<IEnumerable<NodeInfo>>(NodeManagerRemoved);
                var cleanup = message.Get<bool>(NodeManagerCleanup);
                // Cache all nodes info on borg.
                if (cleanup)
                {
                    _nodesInfo.Clear();
                    removed = new List<NodeInfo>(_nodesInfo);
                }
                else
                    _nodesInfo.RemoveAll(p => removed.Any(r => p?.NodeId == r.NodeId));

                var addedNodes = added as IList<NodeInfo> ?? added.ToList();
                if (added != null)
                {
                    _nodesInfo.RemoveAll(n => addedNodes.Any(o => o.NodeId == n.NodeId));
                    _nodesInfo.AddRange(addedNodes);
                }

                UpdateGroupInfo();
                // Internal broadcast the info to the message bus, so ui can get the updates.
                var managerMessage = new NodesChanges
                {
                    Added = addedNodes,
                    Removed = removed,
                    All = new List<NodeInfo>(_nodesInfo)
                };
                _nodesSubject.OnNext(managerMessage);
                _groupSubject.OnNext(_groups.Clone());
            }
        }

        private void UpdateGroupInfo()
        {
            _groups.NodesByGroup.Clear();
            foreach (var nodeInfo in _nodesInfo)
            {
                foreach (var g in nodeInfo.Groups)
                {
                    Dictionary<string, NodeInfo> value;
                    if (_groups.NodesByGroup.TryGetValue(g, out value))
                    {
                        if (value.ContainsKey(nodeInfo.NodeId)) continue;
                        value.Add(nodeInfo.NodeId, nodeInfo);
                    }
                    else
                    {
                        _groups.NodesByGroup.Add(g, new Dictionary<string, NodeInfo>());
                        _groups.NodesByGroup[g].Add(nodeInfo.NodeId, nodeInfo);
                    }
                }
            }
            //var removeList =
            //    (from i in _groups.NodesByGroup
            //     where !_nodesInfo.SelectMany(nodeInfo => nodeInfo.Groups).Contains(i.Key)
            //     select i.Key).ToList();
            //foreach (var i in removeList) _groups.NodesByGroup.Remove(i);
        }
        #endregion

        #region Borg/Hub Filter
        /// <summary>
        ///     Allows all messages to pass, just add new info to them, or removes so they don't get into the borgs by mistake.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sender"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool AllowMessage(INyxMessage message, INyxNode sender, out string error)
        {
            lock (FilterLock)
            {
                error = string.Empty;
                if (sender is INyxBorg)
                {
                    if (message.Direction == MessageDirection.Out)
                        PrepareNodeInfo(message, sender);
                    return true;
                }
                var msg = message.AsReadOnly();
                // We use a queue
                _tasker.QueueTask(() =>
                {
                    try
                    {
                        RunNodeCollector(msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error processing node info.", ex);
                    }
                });
            }
            return true;
        }

        /// <summary>
        ///     Processes all messages, in all type of <see cref="INyxNode" />.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool CanFilter(INyxNode node, INyxMessage message = null)
        {
            return true;
        }

        private void RunNodeCollector(INyxMessage message)
        {
            var info = message.Get<NodeInfo>(NodeManagerInfo);
            if (info == null) return;
            var sw = Stopwatch.StartNew();
            using (Disposable.Create(() =>
            {
                _logger.Trace("Hub node collector took {0}ms.", sw.ElapsedMilliseconds);
                sw.Stop();
            }))
            {
                if (message.Action == NodesPingStop)
                {
                    _logger.Debug("Received ping stop from {0}", StringExtensions.Trimmer(message.Source));
                    var indexToRemove = _nodesInfo.FindIndex(ni => ni.NodeId == message.Source);
                    if (indexToRemove == -1) return;
                    var nodeToRemove = _nodesInfo.ElementAt(indexToRemove);
                    lock(NodesLock) _nodesInfo.RemoveAt(indexToRemove);
                    lock(UpdaterLock)
                    {
                        foreach (var update in _pushUpdates)
                        SendNodesInfo(null, update, new List<NodeInfo>(), new[] {nodeToRemove});
                    }
                    return;
                }

                bool dirty = false;
                var removed = new List<NodeInfo>();
                var added = new List<NodeInfo>();
                var index = _nodesInfo.FindIndex(ni => ni.NodeId == info.NodeId);
                if (index == -1)
                {
                    _nodesInfo.Add(info);
                    lock (NodesLock) added.Add(info);
                }
                else
                {
                    // Quick check for group changes
                    dirty = _nodesInfo[index].GetHashCode() != info.GetHashCode();
                    if (dirty)
                    {
                        lock (NodesLock) added.Add(info);
                    }
                    _nodesInfo[index] = info;
                }
                // Cleanup 140 seconds old nodes that don't respond.
                lock (NodesLock)
                {
                    _nodesInfo.RemoveAll(ni =>
                    {
                        var res = DateTime.Now.Subtract(ni.TimeStamp).TotalSeconds > 140;
                        if (res) removed.Add(ni);
                        return res;
                    });
                }
                // Remove dead subscriptions
                lock(UpdaterLock) _pushUpdates.RemoveWhere(p => _nodesInfo.All(ni => ni.NodeId != p));
                message.Remove(NodeManagerInfo);
                // Push updates only if something changed.
                if (added.Count == 0 && removed.Count == 0 && !dirty) return;
                _logger.Debug("Added {0} nodes and removed {1}.", added.Count, removed.Count);
                lock (UpdaterLock)
                {
                    foreach (var update in _pushUpdates)
                        SendNodesInfo(null, update, added, removed);

                    // Notify any hub plugin listening to node changes
                    _nodesSubject.OnNext(new NodesChanges {All = _nodesInfo.AsReadOnly(), Added = added.AsReadOnly(), Removed = removed.AsReadOnly()});
                    _groupSubject.OnNext(_groups.Clone());
                }
            }
        }
        #endregion

        /// <summary>
        /// Prepares nodes info
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sender"></param>
        private void PrepareNodeInfo(INyxMessage message, INyxNode sender)
        {
            NodeInfo nodeInfo;
            try
            {
                nodeInfo = NodeInfo.BuildInfo((INyxBorg) sender);
            }
            catch (Exception ex)
            {
                _logger.Error("Error building node info.", ex);
                return;
            }
            var plugs = _pluginManager.GetExtensions().Where(e => e.GetType().IsAssignableTo<IExtendNodeInfo>()).Cast<IExtendNodeInfo>();
            var sw = Stopwatch.StartNew();
            Action<IExtendNodeInfo> act = p =>
            {
                sw.Restart();
                try
                {
                    p.AddExtraData(nodeInfo);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error adding extra info from type {p?.GetType().FullName}.", ex);
                }
                _logger.Trace("{0} took {1}ms to add node info.", p.GetType().FullName, sw.ElapsedMilliseconds);
                
            };
            sw.Stop();
            plugs.ForEach(act);
            message.Set(NodeManagerInfo, nodeInfo);
        }

        #region Service methods
        /// <summary>
        ///     Starts the <see cref="Pinger" /> server.
        /// </summary>
        public void Start()
        {
            if (_borg != null)
            {
                Task.Factory.StartNew(Pinger, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            }
        }

        /// <summary>
        ///     Stops the <see cref="Pinger" /> server.
        /// </summary>
        public async void Stop()
        {
            _stop = true;
            _cancellationTokenSource.Cancel(false);
            if(_borg != null) await _borg.SendMessage("nyx", NodesPingStop, true, 2);
        }
        #endregion

        /// <summary>
        ///     Send the info to the requester <see cref="INyxBorg" />.
        /// </summary>
        /// <param name="sourceMessage">Source message</param>
        /// <param name="added">Nodes added.</param>
        /// <param name="removed">Nodes removed.</param>
        /// <param name="cleanup">Send info for nodes cleanup.</param>
        private void SendNodesInfo([NotNull] INyxMessage sourceMessage,
            [NotNull] IEnumerable<NodeInfo> added,
            [NotNull] IEnumerable<NodeInfo> removed, bool cleanup=false)
        {
            SendNodesInfo(sourceMessage, null, added, removed, cleanup);
        }

        /// <summary>
        ///     Send the info to the requester <see cref="INyxBorg" />.
        /// </summary>
        /// <param name="sourceMessage">Source message</param>
        /// <param name="target">Target for receiveing the updates</param>
        /// <param name="added">Nodes added.</param>
        /// <param name="removed">Nodes removed.</param>
        private void SendNodesInfo([CanBeNull] INyxMessage sourceMessage, [CanBeNull] string target,
            [NotNull] IEnumerable<NodeInfo> added, [NotNull] IEnumerable<NodeInfo> removed, bool cleanup=false)
        {
            _logger.Debug("Pushing node updates to {0}", target);
            var msg = string.IsNullOrWhiteSpace(target)
                ? sourceMessage.Reply(NodesInfoReport)
                : new NyxMessage(target, NodesInfoReport, "Nyx");
            msg.Set(NodeManagerAdded, added.ToArray());
            msg.Set(NodeManagerRemoved, removed.ToArray());
            msg.Set(NodeManagerCleanup, cleanup);
            _hub.BroadcastMessage(msg);
        }

        /// <summary>
        ///     Use this to build the proper message to request info, from the <see cref="INyxHub" />.
        /// </summary>
        [ExtensionAction(NodeManagerNodes)]
        public IObservable<Unit> RequestInfo(bool observe = false, int timeout = 5)
        {
            if (_borg == null) return Observable.Return(Unit.Default);
            Action act = async () =>
            {
                var update = WhenNodesChange
                    .Amb(Observable.Return(NodesChanges.Empty).Delay(TimeSpan.FromSeconds(timeout)));
                var msg = new NyxMessage("Nyx", NodesInfo, _borg.NodeId);
                await msg.SendMessage(_borg);
                await update;
            };
            if (observe)
                return Observable.Start(act);
            new NyxMessage("Nyx", NodesInfo, _borg.NodeId).SendMessage(_borg);
            return Observable.Return(Unit.Default);
        }

        /// <summary>
        ///     Use this to subscribe to automatic updates.
        /// </summary>
        [ExtensionAction(NodeManagerSubscribeUpdates)]
        public void SubscribeUpdates()
        {
            if (_borg == null) throw new NullReferenceException("This must run on a borg.");
            NyxMessage.Create("nyx", NodesUpdateSubscribe, _borg.NodeId).SendMessage(_borg);
            _subscribed = true;
            _subscriptionDisposable.Disposable = _internalHubWatch
                .Where(o => o)
                .Throttle(TimeSpan.FromSeconds(10))
                .ObserveOnPool()
                .Subscribe(o =>
                {
                    _logger.Debug("Subscribe update from hub after lost connection.");
                    NyxMessage.Create("nyx", NodesUpdateSubscribe, _borg.NodeId).SendMessage(_borg);
                });
        }

        /// <summary>
        ///     Pinger service.
        /// </summary>
        private async void Pinger()
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "Nyx Core NodeManager Pinger";
            while (!_stop)
            {
                try
                {
                    _ = _borg.SendMessage(new NyxMessage("Nyx", NodesPing, _borg.NodeId), true);
                    await Task.Delay(TimeSpan.FromSeconds(120), _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Get the group count.
        /// </summary>
        /// <param name="target">The group to check count.</param>
        /// <returns>The observable to watch for notifications when done.</returns>
        public IObservable<int> GetCount(string target)
        {
            return Observable.Start(() =>
            {
                RequestInfo(true).Wait();
                return _groups?.GetCount(target) ?? 0;
            });
        }

        public class GroupsInfo : ICloneable
        {
            internal readonly Dictionary<string, Dictionary<string, NodeInfo>> NodesByGroup =
                new Dictionary<string, Dictionary<string, NodeInfo>>();

            public IEnumerable<string> Groups => NodesByGroup.Where(k => k.Value.Count != 0).Select(k => k.Key);
            public static GroupsInfo Empty => new GroupsInfo();

            public GroupsInfo()
            {
            }

            public GroupsInfo(GroupsInfo groupsInfo)
            {
                NodesByGroup = new Dictionary<string, Dictionary<string, NodeInfo>>(groupsInfo.NodesByGroup);
            }

            public int GetCount(string group)
            {
                Dictionary<string, NodeInfo> value;
                return !NodesByGroup.TryGetValue(group, out value) ? 0 : value.Count;
            }

            public int GetCountByHost(string group, string host)
            {
                Dictionary<string, NodeInfo> value;
                return !NodesByGroup.TryGetValue(group, out value) ? 0 : value.Count(n => n.Value?.HostApp == host);
            }

            object ICloneable.Clone()
            {
                return Clone();
            }

            public GroupsInfo Clone()
            {
                return new GroupsInfo(this);
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var nodeInfo in NodesByGroup.SelectMany(dict => dict.Value))
                {
                    sb.AppendLine(nodeInfo.Value.ToString());
                }
                return sb.ToString();
            }
        }

        public void AddExtraData(NodeInfo nodeInfo)
        {
            if (_computerInfo == null) return;
            nodeInfo.Set("NodeTotalRam", _computerInfo.TotalRam/1024.0/1024.0);
            nodeInfo.Set("NodeFreeRam", _computerInfo.FreeRam/1024.0/1024.0);
            nodeInfo.Set("NodeCpuSpeed", _computerInfo.CpuSpeed);
            nodeInfo.Set("NodeOS", _computerInfo.OS);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _subscriptionDisposable.Dispose();
        }

        /// <summary>
        ///     Tries to get a node name from the node id.
        /// </summary>
        /// <param name="nodeId">Node Id</param>
        public string GetNodeName(string nodeId)
        {
            lock(NodesLock)
                return _nodesInfo
                    .Where(x => x.NodeId == nodeId)
                    .Select(x => x.Name)
                    .FirstOrDefault();
        }
    }


    /// <summary>
    ///     Message for <see cref="IMessageBus" /> subscribers.
    /// </summary>
    public class NodesChanges
    {
        public IEnumerable<NodeInfo> Added { get; set; }
        public IEnumerable<NodeInfo> Removed { get; set; }
        public IEnumerable<NodeInfo> All { get; set; }

        public NodesChanges()
        {
            Added = new List<NodeInfo>();
            Removed = new List<NodeInfo>();
            All = new List<NodeInfo>();
        }

        [JsonIgnore]
        public static NodesChanges Empty => new NodesChanges();
    }

    /// <summary>
    ///     Helper class for building info.
    /// </summary>
    public class NodeInfo : DynamicObject
    {
        [JsonProperty("Name")]
        private string _name;
        [JsonProperty("Ip")]
        private string _ip;
        [JsonProperty("HostApp")]
        private string _hostApp;
        [JsonProperty("HostMode")]
        private NyxHostMode _hostMode;
        [JsonProperty("TimeStamp")]
        private DateTime _timeStamp;
        [JsonProperty("Groups")]
        private IEnumerable<string> _groups;
        [JsonProperty("NodeId")]
        private string _nodeId;
        [JsonProperty("Data", ItemTypeNameHandling = TypeNameHandling.All)]
        private Dictionary<string, object> _data;

        public NodeInfo()
        {
            
        }

        private NodeInfo(string name, string nodeId, string hostApp, IEnumerable<string> list, DateTime now, NyxHostMode hostMode, string ipA)
        {
            _name = name;
            _nodeId = nodeId;
            _hostApp = hostApp;
            _groups = new List<string>(list);
            _timeStamp = now;
            _hostMode = hostMode;
            _ip = ipA;
            _data = new Dictionary<string, object>();
        }

        protected bool Equals(NodeInfo other)
        {
            if (!string.Equals(Name, other.Name)) return false;
            if (!string.Equals(NodeId, other.NodeId)) return false;
            if (!string.Equals(Ip, other.Ip)) return false;
            if (!Equals(Groups, other.Groups)) return false;
            if (!string.Equals(HostApp, other.HostApp)) return false;
            return HostMode == other.HostMode && Equals(_data, other._data);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return HashCode
                    .Of(Name)
                    .And(NodeId)
                    .And(Ip)
                    .And(HostApp)
                    .And((int) HostMode)
                    .AndEach(Groups)
                    .AndEach(_data.Keys)
                    .AndEach(_data.Values);
            }
        }

        [JsonIgnore]
        public string Name => _name;

        [JsonIgnore]
        public string NodeId => _nodeId;

        [JsonIgnore]
        public string Ip => _ip;

        [JsonIgnore]
        public IEnumerable<string> Groups => _groups;

        [JsonIgnore]
        public string HostApp => _hostApp;

        [JsonIgnore]
        public NyxHostMode HostMode => _hostMode;

        [JsonIgnore]
        public DateTime TimeStamp => _timeStamp;

        [JsonIgnore]
        public IReadOnlyDictionary<string, object> Elements => new ReadOnlyDictionary<string, object>(_data);
        /// <summary>
        ///     Add data to the message
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        public NodeInfo Set(string key, object val)
        {
            if (_data.ContainsKey(key)) return this;
            _data[key] = val;
            return this;
        }

        /// <summary>
        ///     Gets a Typed data from the Data dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            var defaultData = default(T);
            if (!_data.ContainsKey(key)) return defaultData;
            var o = _data[key];
            if (o is string && typeof(T) != typeof(string))
            {
                // Cast to string any type.
                var c = TypeDescriptor.GetConverter(typeof(T));
                try
                {
                    o = c.ConvertFromInvariantString(o.ToString());
                }
                catch (Exception)
                {
                    o = defaultData;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                // Serializer makes number long type, so convert them to int in a safe way.
                o = Convert.ToInt32(o);
            }
            else if (typeof(T).IsEnum)
            {
                // Proper cast to enum
                o = Enum.ToObject(typeof(T), o);
            }
            try
            {
                return (T)o;
            }
            catch (Exception e)
            {
                Debug.Write($"[Nyx] NodeInfo.GetData error. {e}");
                return defaultData;
            }
        }

        /// <summary>
        ///     Tries to get the element for this key.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="key">Key</param>
        /// <param name="prop">Data to get.</param>
        /// <returns></returns>
        public bool TryGet<T>(string key, out T prop)
        {
            prop = default(T);
            if (!_data.ContainsKey(key)) return false;
            prop = Get<T>(key);
            return true;
        }


        public static NodeInfo BuildInfo(INyxBorg borg)
        {
            var hostEntry = Dns.GetHostEntry(string.Empty);
            var ipv4 = hostEntry.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();
            var ipv6 = hostEntry.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);
            var ipA = string.Join(";", !ipv4.Any() ? ipv6.Select(ip => ip.ToString()) : ipv4.Select(ip => ip.ToString()));
            var info = new NodeInfo
            (
                Dns.GetHostName(),
                borg.NodeId,
                borg.HostApp,
                borg.SubscribersChannels,
                DateTime.Now,
                borg.HostMode,
                ipA
                );
            return info;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name: " + Name);
            sb.AppendLine("NodeId: " + NodeId);
            sb.AppendLine("HostApp: " + HostApp);
            sb.AppendLine("Groups: " + string.Join(",", Groups));
            sb.AppendLine("LastNodeInfo: " + TimeStamp.ToLongTimeString());
            sb.AppendLine("HostMode: " + HostMode);
            foreach (var o in _data)
                sb.AppendLine(o.Key + ": " + o.Value);
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((NodeInfo) obj);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (!_data.ContainsKey(binder.Name)) return base.TryGetMember(binder, out result);
            result = _data[binder.Name];
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (base.TrySetMember(binder, value)) return true;
            if (_data.ContainsKey(binder.Name)) return true;
            _data[binder.Name] = value;
            return true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IExtendNodeInfo : INyxExtension
    {
        /// <summary>
        ///     This method receives the nodeinfo.
        /// <remarks>This should be a fast operation.</remarks>
        /// </summary>
        /// <param name="nodeInfo"></param>
        void AddExtraData(NodeInfo nodeInfo);
    }
}