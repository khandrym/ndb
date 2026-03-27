using System;
using System.Threading;

int counter = 0;
string message = "Hello from TestApp";

Console.WriteLine("TestApp started");
Console.WriteLine("PID: " + Environment.ProcessId);

// Lines below are breakpoint targets — line numbers matter!
counter = 1;       // line 11
counter = 2;       // line 12
counter = 3;       // line 13
message = "Updated"; // line 14

Console.WriteLine($"Counter: {counter}, Message: {message}");

// Exception test target
try
{
    throw new InvalidOperationException("Test exception");
}
catch (Exception ex)
{
    Console.WriteLine($"Caught: {ex.Message}");
}

// Keep alive for debugging
Thread.Sleep(Timeout.Infinite);
