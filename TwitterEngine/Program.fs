#if INTERACTIVE
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#endif

open System
open System.Text
open System.Diagnostics
open System.Security.Cryptography
open Akka.FSharp
open Akka.Remote
open Akka.Routing
open Akka.Actor
open System.Threading

type Instructions=
    | RegisterAccount of string*string
    | Test
    | Login of string*string
    | Logout of string
    | PrintInfo


let TwitterServer  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    printfn "abc"
    let rec loop() = actor{
        let! message = mailbox.Receive()

        


        return! loop()
    }
    loop()

let UsersActor (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    let mutable userPasswordMap = Map.empty
    let mutable activeUsersSet = Set.empty
    let rec loop() = actor {
        let! message = mailbox.Receive()

        match message with 
        | RegisterAccount (userid, password) ->
            printfn "%s %s" userid password
            // Hash the password later
            userPasswordMap <- userPasswordMap.Add(userid, password)
 

        | Login (userid, password) ->
            if userPasswordMap.ContainsKey(userid) then
                if (password = userPasswordMap.[userid]) then
                    printfn "Welcome %s!! Login Successful"  userid
                    activeUsersSet <- activeUsersSet.Add(userid)
                    // send a token without which user cannot do further actions
                else 
                    printfn "Wrong password"
            else 
                printfn "Username doesn't exist"

        | Logout userid ->
            activeUsersSet <- activeUsersSet.Remove(userid)
            // destroy token at the client side

        | PrintInfo ->
            printfn "Active users are %A" activeUsersSet
            printfn "All users are %A" userPasswordMap



        
        | Test ->
            printfn "test"
            


        return! loop()
    }
    loop()





[<EntryPoint>]
let main argv =
    let nodes = (int) argv.[0];
    let twitterSystem = ActorSystem.Create("twitterSystem");
    let twitterServerRef = spawn twitterSystem "twitterServerRef" (TwitterServer twitterSystem);
    let usersRef = spawn twitterSystem "usersRef" (UsersActor twitterSystem);
    usersRef <! RegisterAccount("prasad", "prasad")
    usersRef <! RegisterAccount("vaishnavi", "vaishnavi")
    usersRef <! Login("prasad", "prasad")
    usersRef <! Login("vaishnavi", "vaishnavi")
    usersRef <! Logout("prasad")
    usersRef <! PrintInfo
    System.Console.ReadKey() |> ignore
    0