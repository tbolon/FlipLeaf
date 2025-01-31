using System.Reflection;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var version = Assembly
    .GetEntryAssembly()?
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    .ToString();
Console.WriteLine($"🍃 Hello World! v{version}");