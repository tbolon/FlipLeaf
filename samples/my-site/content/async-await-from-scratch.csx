#!/usr/bin/env dotnet-script
using System.Threading;
using System.Collections.Concurrent;

async_local();

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
        var value = i;
        // cette ligne demande au thread pool de mettre en file d'attente un traitement
        ThreadPool.QueueUserWorkItem(delegate
        {
            Console.WriteLine(value);
            Thread.Sleep(1000);
        });
    }

    Console.ReadKey(true);
}

public static void no_capture()
{
    for (int i = 0; i < 100; i++)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            Console.WriteLine(i);
            Thread.Sleep(1000);
        });
    }

    Console.ReadKey(true);
}

public static void async_local()
{
    AsyncLocal<int> local = new AsyncLocal<int>();
    for (int i = 0; i < 100; i++)
    {
        local.Value = i;
        MyThreadPool.QueueUserWorkItem(delegate
        {
            Console.WriteLine(local.Value);
            Thread.Sleep(1000);
        });
    }

    Console.ReadKey(true);
}

internal static class MyThreadPool
{    
    private static readonly BlockingCollection<Action> s_workItems = new();

    public static void QueueUserWorkItem(Action action) => s_workItems.Add(action);

    static MyThreadPool()
    {
        // on lance autant de threads que le nombre de processeurs logiques
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            // démarrage d'un thread en arrière-plan chargé de dépiler les tâches à effectuer
            new Thread(() => {
                while (true)
                {
                    Action workItem = s_workItems.Take();
                    workItem();
                }
             }) { IsBackground = true /* */ }.Start();
        }
    }
}