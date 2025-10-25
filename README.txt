Multiplayer demo package
========================

Contents:
- Server/: ASP.NET Core project that runs LiteNetLib UDP server on port 9050 and hosts a SignalR hub at /networkHub.
- Client/: Raylib-cs based client that connects to server and renders simple circles. Use multiple instances for multiple players.
- Dashboard/: Simple static HTML+JS that connects to the SignalR hub and visualizes packet events and player positions.

How to run
----------
You need .NET 8 SDK installed and (for the client) native dependencies for Raylib-cs.
From a terminal:

1) Run the server:
   cd Server
   dotnet restore
   dotnet run

   The server starts Kestrel on the default port (usually 5000 or 5001 for HTTPS).
   It also starts the LiteNetLib UDP server bound to port 9050.

2) Open the dashboard:
   - If the server is running on the same machine, open in your browser:
     http://localhost:5000/index.html
   - (Alternatively) Serve Dashboard/index.html from any static host that is allowed to connect to http://localhost:5000/networkHub

3) Run one or more clients:
   cd Client
   dotnet restore
   dotnet run
   Enter a player id when prompted. You can run multiple clients on the same machine (multiple consoles).

Notes / Caveats
- This is a minimal educational prototype for demonstrations.
- For production or larger demos, consider packaging the dashboard into the ASP.NET server or adding CORS settings.
- Raylib native libs may need to be installed for Raylib-cs to work. On Windows, the NuGet package should bring the native binaries; on Linux/Mac you may need raylib installed.

Files were generated automatically. Feel free to modify for your demonstration needs.
