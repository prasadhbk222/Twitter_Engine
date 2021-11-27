// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Akka.FSharp
open Akka.Remote
open Akka.Actor
open Myconfig

// Instructions
type ConnectionIns = 
    | RequestConnection
    | ConnRequest

let ClientConn (clientSystem:ActorSystem) (mailbox: Actor<_>) =
    let url = "akka.tcp://twitterSystem@localhost:8080/user/twitterServerRef"
    let conn = select url clientSystem 
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | RequestConnection ->
            printfn "RequestConnection Instruction called!"
            conn <! ("Register", "one")

        

        return! loop()
    }
    loop()

[<EntryPoint>]
let main argv =
    let clientSystem  = System.create "ClientSystem" config // Client System Initialization
    let clientConnRef = spawn clientSystem "clientConnRef" (ClientConn clientSystem ) // Connection Actor Initialization
    
    clientConnRef <! RequestConnection

    System.Console.ReadLine() |> ignore
    0 // return an integer exit code