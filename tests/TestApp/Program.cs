using System;
using System.Threading;

Console.WriteLine("TestApp started");
Console.WriteLine("PID: " + Environment.ProcessId);

// Keep alive for debugging
Thread.Sleep(Timeout.Infinite);
