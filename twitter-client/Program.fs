// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Akka.FSharp
open Akka.Remote
open Akka.Actor
open Myconfig

open System.Threading
open System.Collections.Generic

// Instructions
type ConnectionIns = 
    | RequestConnection
    | ConnRequest

type UsersActorInstructions=
    | RegisterAccount of string*string
    | Test
    | Login of string*string
    | Logout of string
    | PrintInfo
    | GetFollowers of string*int
    | GetFollowersForRetweet of string*string*int //user originuser and tweetid
    | Follow of string*string //userid // useridoffollowed
    | FollowHashTag of string*string //userid //hashtag
    | GetFollowersOfHashTag of string*int*string //userid //tweetid//hashtag 
 
type TweetsActorInstructions=
    | Tweet of string*string   //userid //tweet
    | ReTweet of string*string*int //userid //copyfromuserid //tweetid
    | ReceiveFollowers of string*int*List<string> //userid/ //tweetid //listoffollowers
    | ReceiveFollowersForRetweet of string*int*string*List<String>  //userid //tweetid //originUserid // followerlist
    | ReceiveFollowersOfHashTag of string*int*List<string> //userid/ //tweetid //listofhashtagfollowers
    | ReceiveHashTags of string*int*List<string>  //userid //tweetid //Listoffollowers
    | ReceiveMentions of string*int*List<string>  //userid //tweetid //Listoffollowers
    | QueryByHashTagOrMention of string*string   //userid // hashtag or mention


//@@@@@@@@@@@@@@@ write code to query tweets based on hashtags and mentions

type TweetsSenderActorInstruction=
    | SendTweet of string*string*List<String> // tweet and recipientList
    | Query of List<String>*List<string>*string            // tweet list, tweeter list and userid 
    | SendRetweet of string*string*string*List<String>  //(userId, originUserId, tweet, followerList)

type TweetParser =
    | GetHashTagsAndMentions of string*int*string //tweet

let UserActor (userid:string) (password:string) (clientSystem:ActorSystem) (mailbox: Actor<_>)=
    let url = "akka.tcp://twitterSystem@localhost:8080/user/twitterServerRef"
    let clientConnRef = select url clientSystem

    let rec loop() = actor{
        let! (message:Object) = mailbox.Receive()
        // printfn "Message received from the other side %A" message
        let (instruction, arg1, arg2, arg3, arg4) : Tuple<String,string,string,List<String>, List<String>> = downcast message
        match instruction with
        | "Start" ->
            printfn "%s" mailbox.Self.Path.Name
            clientConnRef <! ("RegisterAccount",mailbox.Self.Path.Name, mailbox.Self.Path.Name,"")
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(1000.0),TimeSpan.FromMilliseconds(1000.0),mailbox.Self, ("Follow", "","", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(2000.0),TimeSpan.FromMilliseconds(2000.0),mailbox.Self, ("Tweet", "","", new List<String>(), new List<String>()))


        | "Follow" ->
            let username = mailbox.Self.Path.Name
            let id = username.Substring(4) |> int
            let followid = Random().Next(1, id)
            if not <| (id = followid) then 
                let followerUserId = "user" + (string)followid
            //printfn "%s" followerUserId
                clientConnRef <! ("Follow", username, followerUserId,"")


        | "Tweet" ->
            clientConnRef <! ("Tweet", mailbox.Self.Path.Name, "Hello followers...gooooood Morning. WITHOUT HASHTAGS","")

        | "ReceiveTweet" ->
            printfn "%s received tweet : %s" mailbox.Self.Path.Name arg1

        | "ReceiveRetweet" ->
            let originUserId = arg2
            printfn "%s received retweet by %s : %s" originUserId mailbox.Self.Path.Name arg1

        
        | "ReceiveQueryResult" ->
            let tweetersList = arg3
            let tweetList = arg4
            let tc = tweetList.Count - 1
            for i in 0..tc do
                printfn "Following tweets were sent to %s : %s : %s"  mailbox.Self.Path.Name tweetersList.[i] tweetList.[i]


            
            
           
            



        return! loop()
    }
    loop()
    
    

let ClientConn numOfUsers (clientSystem:ActorSystem)  (mailbox: Actor<_>) =
    let url = "akka.tcp://twitterSystem@localhost:8080/user/twitterServerRef"
    let twitterServerRef = select url clientSystem 
    let mutable userNumber = 1
    let mutable userId = "dummy"
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | "SpawnUsers" ->
            userId <- "user" + (string)userNumber
            let userRef =  spawn clientSystem userId (UserActor userId userId clientSystem)
            userRef <! ("Start", "","", new List<String>(), new List<String>())
            
            if userNumber < numOfUsers then
                userNumber <- userNumber + 1
                clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0),mailbox.Self, "SpawnUsers")




        // | RequestConnection ->
        //     printfn "RequestConnection Instruction called!"
        //     //conn <! RegisterAccount("gauri", "gauri")
        //     //conn <! ("abcd", "pqrs", "xyz")
        //     clientConnRef <! ("RegisterAccount","gauri", "gauri","")
        //     clientConnRef <! ("RegisterAccount","prasad", "prasad","")
        //     clientConnRef <! ("RegisterAccount","vaishnavi", "vaishnavi","")
        //     clientConnRef <! ("RegisterAccount","siddhi", "siddhi","")
        //     clientConnRef <! ("RegisterAccount","nikhil", "nikhil","")
        //     clientConnRef <! ("RegisterAccount","arijit", "arijit","")

        //     Thread.Sleep(1000)
        //     clientConnRef <! ("Login","prasad", "prasad","")
        //     clientConnRef <! ("Login","vaishnavi", "vaishnavi","")
        //     clientConnRef <! ("Login","siddhi", "siddhi","")
        //     clientConnRef <! ("Login","nikhil", "nikhil","")
        //     clientConnRef <! ("Login","arijit", "arijit","")

        //     Thread.Sleep(1000)
        //     clientConnRef <! ("Follow","vaishnavi", "prasad","")
        //     clientConnRef <! ("Follow","siddhi", "prasad","")
        //     clientConnRef <! ("Follow","arijit", "vaishnavi","")
        //     clientConnRef <! ("Follow","siddhi", "vaishnavi","")

        //     Thread.Sleep(1000)
        //     clientConnRef <! ("FollowHashTag","nikhil", "#Mumbai","")
        //     clientConnRef <! ("FollowHashTag","prasad", "#Cricket","")

        //     Thread.Sleep(1000)
        //     clientConnRef <! ("Tweet","prasad", "Hello followers...gooooood Morning. WITHOUT HASHTAGS","")
        //     clientConnRef <! ("Tweet","prasad", "Hello followers...gooooood Morning #Mumbai #India","")

        //     Thread.Sleep(1000)
        //     clientConnRef <! ("Tweet","nikhil", "#Cricket is back #ipl @siddhi" ,"")
        //     Thread.Sleep(1000)
        //     clientConnRef <! ("QueryByHashTagOrMention", "arijit", "#Cricket", "")
        //     Thread.Sleep(1000)
        //     clientConnRef <! ("QueryByHashTagOrMention", "vaishnavi", "@siddhi", "")
        //     Thread.Sleep(1000)
        //     clientConnRef <! ("ReTweet", "vaishnavi", "nikhil", "3");
        //     clientConnRef <! ("Logout", "prasad", "", "")
            // clientConnRef <! PrintInfo
            //conn <! RegisterAccount("gauri", "gauri")

        

        return! loop()
    }
    loop()

[<EntryPoint>]
let main argv =
    let numOfUsers = (int) argv.[0]
    let clientSystem  = System.create "clientSystem" config // Client System Initialization
    let clientConnRef = spawn clientSystem "clientConnRef" (ClientConn numOfUsers clientSystem ) // Connection Actor Initialization
    
    clientConnRef <! "SpawnUsers"
    // clientConnRef <! RegisterAccount("prasad", "prasad")
    // clientConnRef <! RegisterAccount("vaishnavi", "vaishnavi")
    // clientConnRef <! RegisterAccount("siddhi", "siddhi")
    // clientConnRef <! RegisterAccount("nikhil", "nikhil")
    // clientConnRef <! RegisterAccount("arijit", "arijit")
    // Thread.Sleep(1000)
    // clientConnRef <! Login("prasad", "prasad")
    // clientConnRef <! Login("vaishnavi", "vaishnavi")
    // clientConnRef <! Login("siddhi", "siddhi")
    // clientConnRef <! Login("nikhil", "nikhil")
    // clientConnRef <! Login("arijit", "arijit")
    // Thread.Sleep(1000)
    // clientConnRef <! Follow("vaishnavi", "prasad")
    // clientConnRef <! Follow("siddhi", "prasad")
    // clientConnRef <! Follow ("arijit", "vaishnavi")
    // clientConnRef <! Follow ("siddhi", "vaishnavi")
    // Thread.Sleep(1000)
    // clientConnRef <! FollowHashTag("nikhil", "#Mumbai")
    // clientConnRef <! FollowHashTag("prasad", "#Cricket")
    // Thread.Sleep(1000)
    // clientConnRef <! Tweet("prasad", "Hello followers...gooooood Morning. WITHOUT HASHTAGS")
    // clientConnRef <! Tweet("prasad", "Hello followers...gooooood Morning #Mumbai #India")
    // Thread.Sleep(1000)
    // clientConnRef <! Tweet("nikhil", "#Cricket is back #ipl @siddhi" )
    // Thread.Sleep(1000)
    // clientConnRef <! QueryByHashTagOrMention("arijit", "#Cricket")
    // Thread.Sleep(1000)
    // clientConnRef <! QueryByHashTagOrMention("vaishnavi", "@siddhi")
    // Thread.Sleep(1000)
    // clientConnRef <! ReTweet("vaishnavi", "nikhil", 3);
    // clientConnRef <! Logout("prasad")
    // clientConnRef <! PrintInfo

    System.Console.ReadLine() |> ignore
    0 // return an integer exit code