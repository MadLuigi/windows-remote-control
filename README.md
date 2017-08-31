# windows-remote-control
This project was developed for System and Device Programming class during my master's degree. It's a client-server solution for remote control of Windows computers.

The server, coded in C++, retrieves the list of windows belonging to applications running on the host system, information about related processes and which window has focus. At first, the server sends the list to an eventually connected client, then it notifies the client about significant events such as focus changes and closing/opening of applications. In addition, the server can execute keys combinations (which might eventually include Ctrl/Alt/Shift modifiers key), received from the client, on running applications.

The client is a GUI application coded in C#. After connection to a server is established, it retrieves and displays the list of running applications together with the related process icon. It also monitors focus time of each application since connection to server started. The client can connect to many servers displaying statistics about each connected server and can send keys combinations to all servers where is running a certain application.
