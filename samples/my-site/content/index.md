---
title: Mon site
layout: default
machin: hopddddffdd
---
# Writing async await from scratch in C# with Stephen Toub

Disclaimer : cet article est une repompe totale de l'excellente vidéo [Writing async/await from scratch in C# with Stephen Toub](https://www.youtube.com/watch?v=R-z2Hv-7nxk) à destination des francophones sous forme d'un exercice de résumé et de retranscription.

Async/await est un pattern qui existe en .NET depuis 12 ans environ (2010).

Ce pattern a changé la manière dont les développeurs interagissent avec le code asynchrone et gèrent la concurrence.
C'est aussi l'un des plus dangereux, dans le sens où il permet facilement de se tirer une balle dans le pied (sans même s'en apercevoir).

Durant ce (long) article nous allons recréer le pattern await/async de zéro en c# pour comprendre comment il fonctionne.

Comme le dit Stephen en introduction de sa vidéo, une chose que j'apprécie particulièrement en tant que développeur c'est lorsque j'arrive à avoir un modèle mental de la manière dont fonctionne les choses. Ne pas avoir forcément besoin de tout comprendre dans le détail, chaque ligne de code de quelque chose dont vous vous servez. Mais plus vous en savez sur le fonctionnement interne, mieux vous êtes capable de vous en servir et d'en tirer le maximum.

Et c'est donc le cas pour le pattern async/await. Il existe depuis de nombreuses années et on est désormais habitués à l'utiliser. Mais en réalité très peu savent réellement comment il fonctionne réellement et les raisons des nuances et contraintes qu'il impose sur notre manière de coder.

Attachez vos ceintures, c'est parti : nous nous téléportons plus de 10 ans dans le passé, et nous allons implémenter de zéro le pattern async/await (dans une version simplifié).

Nous ne ferons pas attention aux performances.

## La base : concurrence et asynchronisme

Le premier concept fondamental à aborder lorsque l'on parle d'asynchronisme est la **concurrence**&nbsp;: vous démarrez une tâche qui peut se terminer immédiatement ou bien plus tard mais vous ne le savez pas. Vous l'avez simplement démarrée puis votre code continue d'effectuer d'autres traitements.

Pendant ce temps la tâche que vous avez démarrée s'exécute et lance même peut-être d'autres traitements. Vous vous retrouvez avec plusieurs traitements qui s'exécutent en même temps.

À la base de ce fonctionnement vous avez le *thread pool*, chargé de faire s'exécuter ces tâches chacune de leur côté.
Donc l'une des premières tâches que nous allons devoir réaliser va consister à recréer un thread pool en .NET.

Prenons l'exemple suivant :

```csharp
for (int i = 0; i < 1000; i++)
{
    // cette ligne demande au thread pool de mettre en file d'attente un traitement
    ThreadPool.QueueUserWorkItem(delegate { /* traitement s'exécutant en fond */ })
    // et immédiatement aprés, je peux effectuer un autre traitement
}
Console.ReadLine();
```

Dans cet exemple j'ai un traitement (mon delegate) qui s'exécute de manière asynchrone : je l'ai lancé puis j'ai continué mon traitement.
Cela ne signifie pas pour autant que le traitement s'exécutera en **concurrence** avec mon code actuel.

Par exemple, dans du code WinForms, si vous lancez ce traitement sur le thread graphique (le thread principal de l'UI) via un appel de `Control.BeginInvoke`, le traitement sera remis en file d'attente sur le thread graphique. Vous aurez bien lancé un traitement asynchrone, mais il ne s'exécutera pas en concurrence avec votre code actuel : il s'exécutera **après** par le **même** thread. 

La concurrence se produira lorsque le code qui s'exécute dans votre delegate s'exécute en même temps (dans un autre thread), que le code qui se situe après l'instruction `QueueUserWorkItem`. Vous ne maîtrisez pas non plus cette concurrence : elle dépendra de votre matériel (nombre de processeurs), de la charge de travail, de milliers d'autres paramètres que vous ne maîtrisez pas.

```csharp
public void Button1_Click(object sender, EventArgs e)
{
    for (int i = 0; i < 1000; i++)
    {
        // cette ligne demande au thread pool de mettre en file d'attente un traitement
        ThreadPool.QueueUserWorkItem(delegate {
            Control.BeginInvoke(this, new MethodInvoker(delegate { Console.WriteLine(i); }, null);
        });
        // et immédiatement aprés, je peux effectuer un autre traitement
    }
}
```

Donc vous ne pouvez pas avoir de concurrence sans asynchronisme, alors que vous pouvez avoir de l'asynchronisme sans concurrence.

```csharp
for (int i = 0; i < 1000; i++)
{
    // cette ligne demande au thread pool de mettre en file d'attente un traitement
    ThreadPool.QueueUserWorkItem(delegate {
        Console.WriteLine(i);
        Thread.Sleep(1000);
    })
}
Console.ReadLine();
```

En écrivant ce code, par exemple sur une machine disposant de 12 processeurs logiques, donc avec supposons 12 threads pouvant s'exécuter en paralléle, je pourrais supposer voir s'afficher les nombres de 0 à 11 très vite, puis de 12 à 23, etc.

En réalité, nous allons voir s'afficher uniquement le nombre 1000, sur 1000 lignes.
Car en réalité ce que ce code fait réellement c'est de mettre en file d'attente 1000 traitements d'affichage de la valeur de `i`.
Et le temps que ces traitements se lançent réellement, la variable `i` a été incrémentée pour valoir 1000.

Cela révèle aussi un problème de méconnaissances lié aux closures (portées). En effet, lorsque l'on écrit un delegate, qui se réfère à une variable déclarée en dehors de la portée, c'est une référence vers cette variable qui est utilisée : tous les traitements se réfèrent donc à la même variable qui est incrémentée au fur et à mesure.

Pour obtenir une copie de la variable dédiée au traitement avec la valeur au moment précis où la tâche a été programmer, il faut donc passer par une variable intermédiaire :


```csharp
for (int i = 0; i < 1000; i++)
{
    int capturedVariable = i; // création d'une variable locale pour capturer la valeur courante de i
    ThreadPool.QueueUserWorkItem(delegate {
        Console.WriteLine(capturedVariable);
        Thread.Sleep(1000);
    })
}
Console.ReadLine();
```

Nous aurons bien le résultat attendu qui s'affiche, à savoir tous les nombres s'incrémentant de 0 à 1000.

## Créer notre propre ThreadPool

Dans l'exemple ci-dessous, nous avons utilisé le ThreadPool réel existant en .NET.
Nous allons donc maintenant chercher à créer notre propre version du thread pool.

La signature de notre classe va ressembler à celle du thread pool officiel :

```csharp
static class MyThreadPool
{
    public static void QueueUserWorkItem(Action action)
    {
        // ...
    }
}
```

Note : `Action` et `delegate` représente des fonctions (des pointeurs de fonction).
Les `delegate` sont des pointeurs de fonction qui peuvent prendre n'importe quel forme (nombre et type de paramètre, retour).
Le type `Action` est un type spécifique de delegate (très utilisé) qui ne prend aucun paramètre et ne renvoie rien.

### Stockage et exécution

Nous avons besoin de stocker quelque part ces actions à effectuer, comme l'indique le nom de la méthode :

```csharp
static class MyThreadPool
{
    private static readonly BlockingCollection<Action> s_workItems = new();

    public static void QueueUserWorkItem(Action action)
    {
        // ...
    }
}
```

L'intérêt de la `BlockingCollection<T>` étant qu'elle se comporte comme une `ConcurrentQueue<T>` lorsqu'il s'agit d'ajouter des éléments, mais pour retirer un élément elle devient bloquante : si la liste est vide l'appel sera bloquant.
Ce comportement est exactement celui souhaité pour un thread pool : tous les threads vont tenter de prendre des choses à exécuter depuis ma file d'attente, et lorsqu'il n'y aura rien à traiter ils attendront simplement.

Il faut désormais ajouter le code pour effectuer le traitement.

Il existe deux types de thread en .NET : les background threads (arrière-plan) et foreground threads (premier-plan).
La seule différence entre les deux concerne le moment où la méthode d'entrée de votre programme se termine : est-ce que le process doit attendre que tous les threads qui travaillent encore se terminent ou doit-il les interrompre ?
Le process attendra que les threads foreground (de premier plan) se terminent et les threads background seront interrompus. Si vous avez un thread qui effectue une boucle infinie en attente de traitement, il faudra donc penser à le configurer comme Background Thread.

```csharp
static class MyThreadPool
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
```

Relancer le code exemple en utilisant ce nouveau ThreadPool renverra exactement le même résultat :

```csharp
for (int i = 0; i < 1000; i++)
{
    int capturedVariable = i; // création d'une variable locale pour capturer la valeur courante de i
    MyThreadPool.QueueUserWorkItem(delegate {
        Console.WriteLine(capturedVariable);
        Thread.Sleep(1000);
    });
}
Console.ReadLine();
```

Nous avons donc implémenté un pseudo ThreadPool qui se comporte apparement de la même manière que le ThreadPool .NET.

En vérité si vous examinez un peu le code du ThreadPool .NET réel vous verrez qu'il est en réalité beaucoup plus complexe. Toute cette complexité supplémentaire tourne principalement autour de deux points :

1. Le rendre le plus efficache possible (allocations mémoire et performances)
2. Avoir un nombre de threads dynamique. Il y a énormément de code afin de déterminer la manière dont le nombre de thread pour traiger les tâches doit augmenter ou diminuer.

Un concept qu'il est nécessaire d'aborder maintenant concerne un détail que beaucoup de développeurs utilisent sans réellement savoir la manière dont cela fonctionne.

Par exemple lorsque vous développez un site en ASP.NET Core, vous utilisez parfois le HttpContext, et vous utilisez HttpContext.Current pour savoir quel est le contexte de la requête ASP.NET courante. Autre exemple lorsque vous utilisez les Principal avec les threads (Thread.Principal), avec des informations sur l'identité associée au thread.

Toutes ces informations ambiantes semblent se répandre et suivre le changement de thread lorque vous programmez une tâche asynchrone, ou que vous continuez un traitement après un traitement précédent asynchrone.

### Execution Context et Thread Local Storage

Mais il faut bien qu'un traitement soit responsable de cette fonctionnalité, et il se trouve que c'est géré via le concept d'Execution Context et de Thread Local Storage

:::note
Il ne faut pas confondre le Thread Local Storage avec la notion de *capture de contexte*.

Lorsque nous avions parlé de la variable locale utilisé pour capturer la valeur courante de `i` il s'agissait d'une fonctionnalité du compilateur qui va créer du code pour que notre soit être disponible dans un endroit du code exécuté dans un thread différent via des paramètres masqués.

Là il est question de rendre des variables disponibles à n'importe quel endroit de l'exécution pour d'autres threads.
:::

Un exemple utilisant le thread local storage est la possibilité de marquer une variable statique avec l'attribut `[ThreadStatic]`, qui permet de garantir que chaque thread possèdera sa propre valeur de la variable (le champ est donc statique pour chaque thread).

Cet attribut permet seulement de dire que chaque thread possèdera sa propre valeur. Cela ne permet pas de faire "suivre" cette valeur lorsqu'un traitement s'exécute sur plusieurs threads comme dans le cas des async/await.

Il existe un autre type pour cela, c'est le type `AsyncLocal<T>`.

```csharp
AsyncLocal<int> myValue = new();
for (int i = 0; i < 1000; i++)
{
    myValue.Value = i;
    ThreadPool.QueueUserWorkItem(delegate
    {
        Console.WriteLine(myValue.Value);
        Thread.Sleep(1000);
    });
}
Console.ReadLine();
```

Lorsque l'on lit ce code on peut avoir l'impression d'avoir le même fonctionnement que dans notre cas plus haut où la variable était partagée et allait provoquer le même problème. Sauf que ce code fonctionne parfaitement, grâce au contexte d'exécution utlisé par ce type `AsyncLocal<T>`.

Le rôle de ce contexte d'exécution est de prendre tout ce qui est stocké dans le stockage spécifique d'un Thread et se charger de le propager lors de tous les appels asynchrones tels que `ThreadPool.QueueUserWorkItem` ou `new Thread` ou `await`.

Et donc, si l'on remplace le code d'exécution précédent pour passer sur notre ThreadPool, on obtient le résultat suviant :

```csharp
AsyncLocal<int> myValue = new();
for (int i = 0; i < 1000; i++)
{
    myValue.Value = i;
    MyThreadPool.QueueUserWorkItem(delegate
    {
        Console.WriteLine(myValue.Value);
        Thread.Sleep(1000);
    });
}
Console.ReadLine();
```

Uniquement des zéros.

Car désormais nous réimplémentons nous-même cette couche de bas-niveau, et nous devons donc aussi réimplémenter ces fonctionnalités.

C'est l'un des aspects intéressants lorsque l'on commence à regarder sous le capot : l'occasion de réorganiser notre modèle mental en ayant compris quel était la fonction de chaque pièce du puzzle.

Pour en revenir à notre cas, plutôt que stocker uniquement l'action à exécuter, nous allons aussi stocker le contexte d'exécution, qui sera transmis lors des échanges entres threads.

```csharp
static class MyThreadPool
{
    // on stocke désormais une instance de ExecutionContext
    private static readonly BlockingCollection<(Action, ExecutionContext?)> s_workItems = new();

    // on capture le contexte d'exécution au moment de programmer l'action
    public static void QueueUserWorkItem(Action action) => s_workItems.Add((action, ExecutionContext.Capture()));

    static MyThreadPool()
    {
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() => {
                while (true)
                {
                    // on récupère le contexte d'exécution
                    (Action workItem, ExecutionContext? context)  = s_workItems.Take();
                    workItem();
                }
             }) { IsBackground = true }.Start();
        }
    }
}
```

:::note
La classe `ExecutionContext` ressemble a un simple `Dictionary<,>` qui est stocké dans la zone de stockage du thread. La classe en elle-même est plus complexe, mais surtout par souci d'optimisation.
:::

L'API Capture() va permettre d'extraire les données d'exécution du thread courant pour la passer à la tâche invoquée.

Il va donc falloir ensuite *restaurer* ce contexte d'exécution pour le rendre disponible sur notre code :


```csharp
static class MyThreadPool
{
    private static readonly BlockingCollection<(Action, ExecutionContext?)> s_workItems = new();

    public static void QueueUserWorkItem(Action action) => s_workItems.Add((action, ExecutionContext.Capture()));

    static MyThreadPool()
    {
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() => {
                while (true)
                {
                    (Action workItem, ExecutionContext? context)  = s_workItems.Take();

                    if (context == null)
                    {
                        // pas de contexte d'exécution
                        workItem();
                    }
                    else
                    {
                        // lancement de l'action
                        ExecutionContext.Run(context, delegate { workItem(); }, null);
                    } 
                }
             }) { IsBackground = true }.Start();
        }
    }
}
```

**Disgression sur nullability et les delegates**

Ok donc nous avons un code qui commence à prendre forme pour nous permettre de programmer des tâches asynchrones qui s'exécutent dans un contexte adapté (elles héritent correctement du contexte parent).

Nous pouvons lancer ces tâches et continuer à faire notre traitement, par contre nous n'avons pas de moyen d'attendre ces tâches, ou d'être au courant lorsqu'elles se terminent.

C'est pour cette raison qu'il reste ce `Console.ReadLine()` qui permet au programme d'attendre la fin de l'exécution avant de se terminer.

Pour pouvoir être informé de l'exécution de ces travaux il nous manque un objet représentant ce travail.

## L'objet Task

Nous allons donc implémenter notre propre version d'une tâche qui va nous permettre d'interagir avec cet asynchronisme.

Cette tâche sera une simple classe, contenant quelques données à propos de notre tâche.

Par exemple le fait de savoir si elle est terminée ou non, via une propriété `IsCompleted`.

Il nous faut donc aussi des méthodes pour indiquer lorsque cette tâche s'est terminée, que ce soit correctement (`SetResult()`), ou avec une erreur (`SetException(Exception ex)`).

```csharp
public class MyTask
{
    public bool IsCompleted { get; }
    public void SetResult() { }
    public void SetException(Exception exception) { }
}
```

:::note
Dans le runtime .NET, cette structure est en réalité découpée en deux classes : Task et TaskCompletionSource. L'objectif est de distinguer la partie uniquement observable (avec `IsCompleted`) de la partie modifiable, ceci afin d'éviter que l'observateur puisse marquer la tâche comme terminée alors qu'il n'en est pas responsable.
:::


**t:13m50s**


## Bibliographies

- [Writing async/await from scratch in C# with Stephen Toub](https://www.youtube.com/watch?v=R-z2Hv-7nxk)
- [How Async/Await Really Works in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/)