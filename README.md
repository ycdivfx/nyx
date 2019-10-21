# Nyx Framework and Plugins

Nyx is a fast network messaging system developed to bridge DCC's. It provides a high level framework that enables broadcasting messages from a node to one or more other nodes. Inlcuded are endpoints for Houdini, 3ds Max, Maya, AE and a general Python lib that enables Nyx in any software that supports Python.

Almost everything in the Nyx system is plugable, from message actions, messaging filtering and internal services. Internally it uses Rx and NetMQ.

# Open source
We are going to open source every plugin we develop for the Nyx framework, like Nyx Voltron (3dsmax<->After Effects), Nyx DBR (3dsmax generic distributed render) and Nyx Sidekick (3dsmax<->Houdini). But this release will happen slowly since there is a lot of code review that needs to happen.

#Some basic info:

Nyx uses a pubisher/subscriber model. Simply put this means a node publishes a message onto a channel and other nodes subscribed to that channel will act up on receiving it.  This can be one-to-many like sending some pyhton to all renderfarm nodes, or one-on-one by sending a message to a node's unique id channel.

A message constists of a 'target channel' to which it is published, an 'action' and a 'payload'. Up on receiving a message the name of the action and the data payload are provided in the event handler.



