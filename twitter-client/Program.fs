// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Akka.FSharp
open Akka.Remote
open Akka.Actor
open Myconfig

open System.Threading
open System.Collections.Generic
open System.IO

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
    //let tweetMap = new Dictionary<int, String>()
    let queueTweetIds = new Queue<int>()
    let queueTweets = new Queue<String>()
    let queueSize = 10;
    let embedWordList = ["#Dosp"; "blackfriday"; "#GatorNights"; "Dosp"; "#blackfriday"; "#GOGATORS"; "DBMS"; "#orangeandblue"; "#DBMS"; "GOGATORS"] 
    let mutable loggedIn:Boolean = false
    let mutable tweetCount = 0
    let mutable retweetCount = 0
    let userName = mailbox.Self.Path.Name
    let path = "UserLogs/" + userName + "_feed.txt"
    let actionPAth = "UserLogs/" + userName + "_action.txt"
    

    let rec loop() = actor{
        let! (message:Object) = mailbox.Receive()
        // printfn "Message received from the other side %A" message
        let (instruction, arg1, arg2, arg3, arg4) : Tuple<String,string,string,List<String>, List<String>> = downcast message
        match instruction with
        | "Start" ->
            File.WriteAllText(path, "")
            File.WriteAllText(actionPAth, "")
            let sendingTime = (mailbox.Self.Path.Name.Substring(4) |> float) * 100.0
            printfn "%s" mailbox.Self.Path.Name
            clientConnRef <! ("RegisterAccount",mailbox.Self.Path.Name, mailbox.Self.Path.Name,"")
            clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0),mailbox.Self, ("Login", "", "", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(1000.0),TimeSpan.FromMilliseconds(1000.0),mailbox.Self, ("Follow", "","", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(2000.0),TimeSpan.FromMilliseconds(sendingTime),mailbox.Self, ("Tweet", "","", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(3000.0),TimeSpan.FromMilliseconds(sendingTime),mailbox.Self, ("ReTweet", "","", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(4000.0),TimeSpan.FromMilliseconds(2000.0),mailbox.Self, ("QueryByHashTagOrMention", "","", new List<String>(), new List<String>()))
            clientSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(4000.0),TimeSpan.FromMilliseconds(1000.0),mailbox.Self, ("FollowHashTag", "","", new List<String>(), new List<String>()))
            
            

        // clientConnRef <! Login("prasad", "prasad")
        | "Login" ->
            File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + userName + " sent a login request \n")
            clientConnRef <! ("Login", mailbox.Self.Path.Name, mailbox.Self.Path.Name, "")

        | "Logout" ->
            if loggedIn = true then
                File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + userName + " sent a logout request \n")
                clientConnRef <! ("Logout", mailbox.Self.Path.Name, mailbox.Self.Path.Name, "")
                clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(10000.0),mailbox.Self, ("Login", mailbox.Self.Path.Name, mailbox.Self.Path.Name, new List<string>() , new List<string>()))
            

        | "Follow" ->
            if loggedIn then
                let username = mailbox.Self.Path.Name
                let id = username.Substring(4) |> int
                let followid = Random().Next(1, id)
                if not <| (id = followid) then 
                    let followerUserId = "user" + (string)followid
                    File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + userName + sprintf " sent follow request to follow %s\n" followerUserId)
                //printfn "%s" followerUserId
                    clientConnRef <! ("Follow", username, followerUserId,"")

        //clientConnRef <! ("FollowHashTag","nikhil", "#Mumbai","")
        | "FollowHashTag" ->
            if loggedIn then
                let index = Random().Next(0, embedWordList.Length - 1)
                let mutable embedWord = embedWordList.[index]
                if  not <| (embedWord.[0] = '#') then
                    embedWord <- "#" + embedWord
                File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + userName + sprintf " sent follow request to follow hashtag %s\n" embedWord)
                clientConnRef <! ("FollowHashTag",mailbox.Self.Path.Name, embedWord,"")


        | "Tweet" ->
            if loggedIn then
                let index = Random().Next(0, embedWordList.Length - 1)
                let embedWord = embedWordList.[index]
                let mutable mention = "abc"
                if index < embedWordList.Length/2 then 
                    let username = mailbox.Self.Path.Name
                    let id = username.Substring(4) |> int
                    let mentionId = Random().Next(1, id)
                    if not <| (id = mentionId) then 
                        mention <- "@user" + (string)mentionId

                File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + userName + " has tweeted " + sprintf "Random hashtag tweet %s and %s \n" embedWord mention)
                clientConnRef <! ("Tweet", mailbox.Self.Path.Name, sprintf "Random hashtag tweet %s and %s" embedWord mention,"")
                tweetCount <- tweetCount + 1
                File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + sprintf "%s has sent %i tweets till now \n" userName tweetCount)
                printfn "%s has sent %i tweets till now" mailbox.Self.Path.Name tweetCount

        | "ReTweet" ->
            if loggedIn then
                let currentQueueLength = queueTweetIds.Count
                if currentQueueLength > 1 then
                    let tweetIdIndex = Random().Next(0, queueTweetIds.Count)
                    let tweetIdArray = queueTweetIds.ToArray()
                    let tweetArray = queueTweets.ToArray()
                    let tweetId = tweetIdArray.[tweetIdIndex]
                    retweetCount <- retweetCount + 1
                    File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + sprintf "%s has retweeted %s having tweetId %i \n" userName tweetArray.[tweetIdIndex] tweetId)
                    File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + sprintf "%s has retweeted %i tweets till now \n" userName retweetCount)
                    clientConnRef <! ("ReTweet", mailbox.Self.Path.Name, (string)tweetId, "")
                    printfn "%s has retweeted %i tweets till now" mailbox.Self.Path.Name retweetCount

        | "ReceiveLoginAck" ->
            printfn "LoginAck rcved: %s" mailbox.Self.Path.Name
            let userId = arg1
            let statusMsg = arg2
            if statusMsg = "success" then
                File.AppendAllText(path, DateTime.Now.ToString() + ":" + userName + " has logged in successfully \n")
                loggedIn <- true
                mailbox.Self <! ("ShowFeed", "", "", new List<String>(), new List<String>())
                clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(10000.0),mailbox.Self, ("Logout", mailbox.Self.Path.Name, mailbox.Self.Path.Name, new List<string>(), new List<string>()))
            else
                loggedIn <- false
                File.AppendAllText(path, DateTime.Now.ToString() + ":" + userName + " couldn't login \n")
                clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(1000.0),mailbox.Self, ("Login", mailbox.Self.Path.Name, mailbox.Self.Path.Name, new List<string>(), new List<string>()))

        | "ReceiveLogoutAck" ->
            loggedIn <- false
            File.AppendAllText(path, DateTime.Now.ToString() + ":" + userName + " has logged out successfully \n")
            clientSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(2000.0),mailbox.Self, ("Login", mailbox.Self.Path.Name, mailbox.Self.Path.Name, new List<string>(), new List<string>()))

        | "ReceiveTweet" ->
            let tweetId = (int) arg2
            let tweet = arg1
            if queueTweetIds.Count > queueSize then
                queueTweetIds.Dequeue()
                queueTweets.Dequeue()
            queueTweetIds.Enqueue(tweetId)
            queueTweets.Enqueue(tweet)

            if loggedIn then
                File.AppendAllText(path, DateTime.Now.ToString() + ":" + sprintf "%s received tweet : %s having tweetid :%s \n " userName arg1 arg2)
                printfn "%s received tweet : %s having tweetid :%s \n " mailbox.Self.Path.Name arg1 arg2

        | "ReceiveReTweet" ->
            let tweetInfo = arg1.Split ':'
            let tweetId  = (int)(tweetInfo.[0])
            let tweet = tweetInfo.[1]
            let originUserId = arg2
            if queueTweetIds.Count > queueSize then
                queueTweetIds.Dequeue()
                queueTweets.Dequeue()
            queueTweetIds.Enqueue(tweetId)
            queueTweets.Enqueue(tweet)
            if loggedIn then
                File.AppendAllText(path, DateTime.Now.ToString() + ":" + sprintf"%s received retweet by %s : %s \n" originUserId mailbox.Self.Path.Name tweet)
                printfn "%s received retweet by %s : %s" originUserId mailbox.Self.Path.Name tweet

        | "ShowFeed" ->
            let mutable feed = ""
            let tweetArray = queueTweets.ToArray()
            for i in [1..queueTweets.Count] do
                feed <- feed + tweetArray.[i-1] + "\n"
            File.AppendAllText(path, DateTime.Now.ToString() + ":" + sprintf"@@@@@@@@@ %s feed: \n %s" userName feed)
            printfn "@@@@@@@@@ %s feed: \n %s" mailbox.Self.Path.Name feed



        //clientConnRef <! ("QueryByHashTagOrMention", "arijit", "#Cricket", "")
        | "QueryByHashTagOrMention" ->
            if loggedIn then
                let index = Random().Next(0, embedWordList.Length - 1)
                let mutable embedWord = embedWordList.[index]
                if  not <| (embedWord.[0] = '#') then
                    embedWord <- "#" + embedWord
                if index < embedWordList.Length/2 then 
                    let username = mailbox.Self.Path.Name
                    let id = username.Substring(4) |> int
                    let mentionId = Random().Next(1, id)
                    if not <| (id = mentionId) then 
                        embedWord <- "@user" + (string)mentionId
                File.AppendAllText(actionPAth, DateTime.Now.ToString() + ":" + sprintf "%s has queried %s \n" userName embedWord)
                clientConnRef <! ("QueryByHashTagOrMention", mailbox.Self.Path.Name, embedWord, "")

        
        | "ReceiveQueryResult" ->
            if loggedIn then
                let tag = arg1
                let tweetersList = arg3
                let tweetList = arg4
                let tc = tweetList.Count - 1
                let mutable output = ""
                printfn "User %s searched for tag %s"  mailbox.Self.Path.Name tag
                for i in 0..tc do
                    output <- output + tweetersList.[i] + tweetList.[i] + "\n"
                    //printfn "User %s searched for tag %s : %s : %s"  mailbox.Self.Path.Name tag tweetersList.[i] tweetList.[i]
                File.AppendAllText(path, DateTime.Now.ToString() + ":" + sprintf"User %s searched for tag %s \n Output : %s \n"  userName tag output)
                printfn "User %s searched for tag %s \n Output : %s"  mailbox.Self.Path.Name tag output


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