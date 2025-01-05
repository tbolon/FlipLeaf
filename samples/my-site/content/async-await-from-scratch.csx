#!/usr/bin/env dotnet-script
using System.Threading;

console();

public static void baseline()
{
    for (int i = 0; i < 1000; i++)
    {
        // cette ligne demande au thread pool de mettre en file d'attente un traitement
        ThreadPool.QueueUserWorkItem(delegate { /* traitement s'exécutant en fond */ });
        // et immédiatement aprés, je peux effectuer un autre traitement
    }

    Console.ReadLine();
}

public static void console()
{
    for (int i = 0; i < 100; i++)
    {
        // cette ligne demande au thread pool de mettre en file d'attente un traitement
        ThreadPool.QueueUserWorkItem(delegate
        {
            Console.WriteLine(i);
            Thread.Sleep(70);
        });
    }

    Console.ReadKey(true);
}