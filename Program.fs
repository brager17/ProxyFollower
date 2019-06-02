// Learn more about F# at http://fsharp.org

open System
open System.Text
open Akka.Actor
open Akka.FSharp
open OpenQA.Selenium.Chrome
open OpenQA.Selenium.Support.UI

open OpenQA.Selenium

open System.IO;
open System.Threading

let system = System.create "system" <| Configuration.load()

type ServerMessage =
    | Request of string
    | RequestComplex of string
type ClientMessage =
    | Reply of string
    | StartWith of IActorRef
    | ReplyToComplex of string
type WorkerMessage =
    | WorkerRequestComplex of string


[<EntryPoint>]
let main argv =
    let windowMaximize (webDriver: ChromeDriver) () =
            webDriver.Manage().Window.Maximize()
    let openPage (webDriver: ChromeDriver) () =
            webDriver.Navigate().GoToUrl "https://free-proxy-list.net/"
    let drobBoxClick (waitDriver: WebDriverWait) () =
            (waitDriver.Until(fun x -> x.FindElement(By.Id "proxylisttable_length")).FindElements(By.TagName "option") |> Seq.last) .Click()
    let getList (waitDriver: WebDriverWait) () =
            (waitDriver.Until(fun x -> x.FindElements(By.TagName "tbody")) |> Seq.last)
                .FindElements(By.TagName("tr"))
            |> Seq.map (fun x -> x.FindElements(By.TagName("tr"))
                                 |> Seq.map (fun x -> x.Text)
                                 |> Seq.toList)
            |> Seq.toList

    let toNextPage (waitDriver: WebDriverWait) () =
            waitDriver.Until(fun x -> x.FindElement(By.Id "proxylisttable_next")).FindElement(By.LinkText("Next")).Click()
    let close (webDriver: ChromeDriver) () =
            webDriver.Quit()


    let system = System.create "system" <| Configuration.load()
    let chromeDriver = new ChromeDriver(Directory.GetCurrentDirectory())
    let waitDriver = new WebDriverWait(chromeDriver, TimeSpan.FromSeconds((float) 10));

    let chromeWindowMaximize = windowMaximize chromeDriver;
    let chromeOpenPage = openPage chromeDriver;
    let chromedrobBoxClick = drobBoxClick waitDriver;
    let chromeGetList = getList waitDriver;
    let chromeToNextPage = toNextPage waitDriver;
    let chromeClose = close chromeDriver;

    chromeWindowMaximize()
    chromeOpenPage()
    chromedrobBoxClick()
    let result = [ 1..4 ] |> List.map (fun x ->
        let list = chromeGetList();
        chromeToNextPage();
        list)
    chromeClose()

    let s = 122;
    let actor1 (mailbox: Actor<_>) =
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match box message with
            | :? string as message -> printf "%s\n" mailbox.Self.Path.Name
            | _ -> "sosi"
            return! loop()
        }
        loop()

    let actor2 (mailbox: Actor<_>) =
        let rec loop() = actor {
            let! message = mailbox.Receive()
            return! loop();
        }
        let pool = spawnOpt system "workItemsProvider" actor1 [ Router(Akka.Routing.RoundRobinPool(10)) ]
        for i in [ 1..10 ] do pool <! "test"
        loop()

    let a2 = spawn system "two" actor2;

    system.WhenTerminated.Wait();
    0 // return an integer exit code
