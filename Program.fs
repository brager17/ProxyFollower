open System
open Akka.Actor
open Akka.FSharp
open OpenQA.Selenium.Chrome
open OpenQA.Selenium.Support.UI
open OpenQA.Selenium
open System.IO;
open FSharp.Data;
open FSharp.Json
open System.Net;
let system = System.create "system" <| Configuration.load()




type input = {
    url: string;
    successHtmlContains: string;
    countProxy: int
 }

type IpAddress =
    {
     Host: string;
     Port: int
    }

let createIpAddress host port =
    { Host = host; Port = port |> Int32.Parse }
type Country = Country of string
type Protocol =
    | Http =1
    | Https=2
    | InCorrect=3

let createProtocol (isHttps: string) =
    match ((isHttps |> Seq.map (fun x -> Char.ToLower(x))) |> Seq.map (string) |> String.concat "") with
    | "no" -> Protocol.Http
    | "yes" -> Protocol.Https
    | _ -> Protocol.InCorrect

type DowloadProxyActorMessage =
    | DowloadProxy

type proxy = {
        IpAddress: IpAddress;
        Country: Country;
        Protocol: Protocol
        }

type CoordinatorActorMessage =
    | Proxies of proxy list

type ProxyAnalyzerActorMessage =
    | Analyze of proxy


[<EntryPoint>]
let main argv =

    let analyze url (ip: IpAddress) =
        let webrequst = Http.AsyncRequestString (
                        url,
                        httpMethod = "GET",
                        customizeHttpRequest = (fun req -> req.Proxy <- WebProxy(sprintf "%s:%i" ip.Host ip.Port, true); req))
        try
        let s = async {
            let! str = webrequst;
            return str } |> Async.RunSynchronously;
        Some s;
        with
        | _ -> None



    let inputText = File.ReadAllText(sprintf "%s/input.json" (Directory.GetCurrentDirectory()))
    let json = Json.deserialize<input> inputText;
    let proxis = Json.deserialize<proxy list> (File.ReadAllText "prox.json")
                |>List.take(2)
                |> List.map (fun x -> analyze json.url x.IpAddress)
                |> List.iter (fun x -> printf "%O" x)


    let getProxies() =
        let windowMaximize (webDriver: ChromeDriver) () =
                webDriver.Manage().Window.Maximize()
        let openPage (webDriver: ChromeDriver) () =
                webDriver.Navigate().GoToUrl "https://free-proxy-list.net/"
        let drobBoxClick (waitDriver: WebDriverWait) () =
                (waitDriver.Until(fun x -> x.FindElement(By.Id "proxylisttable_length")).FindElements(By.TagName "option") |> Seq.last) .Click()
        let getList (waitDriver: WebDriverWait) () =
                (waitDriver.Until(fun x -> x.FindElements(By.TagName "tbody"))
                 |> Seq.head) .FindElements(By.TagName("tr"))
                |> Seq.take 2
                |> Seq.map (fun x ->
                                     let tds = x.FindElements(By.TagName("td")) |> Seq.map (fun x -> x.Text) |> Seq.toArray;
                                     { IpAddress = createIpAddress tds.[0] tds.[1];
                                       Country = tds.[3] |> Country;
                                       Protocol = createProtocol tds.[6] })
                |> Seq.toList

        let toNextPage (waitDriver: WebDriverWait) () =
                waitDriver.Until(fun x -> x.FindElement(By.Id "proxylisttable_next")).FindElement(By.LinkText("Next")) .Click()
        let close (webDriver: ChromeDriver) () =
                webDriver.Quit()


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
        let result = [ 1..4 ] |> List.map (fun _ ->
                   let list = chromeGetList();
                   chromeToNextPage();
                   list) |> List.concat
        chromeClose()
        result

    let readProxyInFile fileName =
       let proxies = Json.serialize (getProxies())
       File.WriteAllText(fileName, proxies)

    readProxyInFile (sprintf "prox.json")

    let getProxyActor (mailbox: Actor<_>) =
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match message with
            | DowloadProxyActorMessage.DowloadProxy -> mailbox.Sender() <! getProxies()
            return! loop();
        }
        loop()

    let proxyAnalyzeActor (mailbox: Actor<_>) =
        let rec loop() = actor {
            let! message = mailbox.Receive()
            match message with
            | Analyze m -> ()

            | _ -> ()
            return! loop();
        }
        loop()

    let coordinatorActor (getProxyActor: IActorRef) (mailbox: Actor<_>) =
       let rec loop() = actor {
            let! message = mailbox.Receive()
            match message with
            | Proxies proxies -> ()
            | _ -> ()
            return! loop()
            }
       let timer = system.Scheduler.ScheduleTellRepeatedly (TimeSpan.Zero, TimeSpan.FromMinutes((float) 10), getProxyActor, DowloadProxyActorMessage.DowloadProxy)
       let pool = spawnOpt system "workItemsProvider" proxyAnalyzeActor [ Router(Akka.Routing.RoundRobinPool(10)) ]

       loop();



    system.WhenTerminated.Wait();
    0 // return an integer exit code
