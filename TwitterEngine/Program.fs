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
open System.Collections.Generic
open System.Collections


open System
open Akka.FSharp
open Akka.Remote
open Akka.Actor
open Myconfig
open System.IO

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
    | ReTweet of string*int //userid //copyfromuserid //tweetid
    | ReceiveFollowers of string*int*Dictionary<string,string> //userid/ //tweetid //listoffollowers
    | ReceiveFollowersForRetweet of string*int*string*Dictionary<string,string>  //userid //tweetid //originUserid // followerlist
    | ReceiveFollowersOfHashTag of string*int*Dictionary<string,string> //userid/ //tweetid //listofhashtagfollowers
    | ReceiveHashTags of string*int*List<string>  //userid //tweetid //Listoffollowers
    | ReceiveMentions of string*int*Dictionary<string,string>  //userid //tweetid //Listoffollowers
    | QueryByHashTagOrMention of string*string   //userid // hashtag or mention


//@@@@@@@@@@@@@@@ write code to query tweets based on hashtags and mentions

type TweetsSenderActorInstruction=
    | SendTweet of string*int*string*Dictionary<string,string> // tweet and recipientList
    | Query of List<String>*List<string>*string*string           // tweet list, tweeter list, userid , tag
    | SendRetweet of string*string*string*Dictionary<string,string>*int  //(userId, originUserId, tweet, followerList)
    | SendLoginToken of string*string   //userid //success or failure
    | SendLogout of string
    | PrintSenderInfo

type TweetParser =
    | GetHashTagsAndMentions of string*int*string //tweet

let hashFunction (hashInput: string) =
    let hashOutput=
        hashInput
        |> Encoding.UTF8.GetBytes
        |> (new SHA256Managed()).ComputeHash
        |> Array.map (fun (x : byte) -> String.Format("{0:X2}", x))
        |> String.concat ""
    hashOutput

let TweetsParserActor  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    let rec loop() = actor{
        let! message = mailbox.Receive()
        printfn "@@@@@@@@@@@@@@@@@@@@@ in tweets parser"
        match message with
        | GetHashTagsAndMentions (userId,tweetId, tweet) ->
            let words = tweet.Split ' '
            let listHashTags = new List<string>()
            //let listMentions = new List<string>()
            let dictMentions = new Dictionary<string, string>()
            for word in words do
                if word.[0] = '#' then
                    listHashTags.Add(word)
                if word.[0] = '@' then
                    dictMentions.Add(word.Substring(1), word.Substring(1))
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            tweetsRef <! ReceiveHashTags(userId, tweetId, listHashTags)
            tweetsRef <! ReceiveMentions(userId, tweetId, dictMentions)


        return! loop()
    }
    loop()

let TweetsSenderActor  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    let url = "akka.tcp://clientSystem@localhost:4000/user/twitterServerRef"
    let StatsFilepath = "ServerLogs/EngineRequestsServed.txt"
    let mutable totalReqServed = 0
    let mutable TweetsServed = 0
    let mutable ReTweetsServed = 0
    let mutable LoginReqServed = 0
    let mutable LogoutReqServed = 0
    let mutable QueriesReqServed = 0
    File.WriteAllText(StatsFilepath, "")

    
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | SendTweet (userid, tweetId, tweet, recipientDict) ->
            
            for recipient in recipientDict do
                let url = "akka.tcp://clientSystem@localhost:4000/user/" + recipient.Key
                let userRef = select url twitterSystem
                totalReqServed <- totalReqServed + 1
                TweetsServed <- TweetsServed + 1
                userRef <! ("ReceiveTweet", sprintf "The tweet by %s =>%s<=having tweetId : %i was sent to %s" userid tweet tweetId recipient.Key , (string)tweetId, new List<String>(), new List<String>())
                printfn "The tweet by %s =>%s<=having tweetId : %i was sent to %s" userid tweet tweetId recipient.Key

            //recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet by %s =>%s<= was sent to %s"  index userid tweet item)
            // recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet =>%s<= was sent to %s" index tweet recipientList.[index])

        | Query(tweetList, tweetersList, user, tag) ->
            totalReqServed <- totalReqServed + 1
            QueriesReqServed <- QueriesReqServed + 1
            // mapping of tweetlist and tweeter list will be done at client according to index
            let url = "akka.tcp://clientSystem@localhost:4000/user/" + user
            let userRef = select url twitterSystem
            userRef <! ("ReceiveQueryResult",tag,"", tweetersList, tweetList)
            let tc = tweetList.Count - 1
            for i in 0..tc do
                printfn "User %s searched for tag %s : %s : %s"  user tag tweetersList.[i] tweetList.[i]

        | SendRetweet (userId, originUserId, tweet, recipientDict, tweetId) ->
            for recipient in recipientDict do
                let url = "akka.tcp://clientSystem@localhost:4000/user/" + recipient.Key
                let userRef = select url twitterSystem
                totalReqServed <- totalReqServed + 1
                ReTweetsServed <- ReTweetsServed + 1
                userRef <! ("ReceiveReTweet", sprintf "%s:The tweet by %s was retweeted by %s =>%s<= was sent to  %s" ((string)tweetId) originUserId  userId tweet recipient.Key 
 , originUserId, new List<String>(), new List<String>())
                printfn "The tweet by %s was retweeted by %s =>%s<= was sent to %s " originUserId  userId tweet recipient.Key 

        

        | SendLoginToken (userId, loginStatus) ->
            totalReqServed <- totalReqServed + 1
            LoginReqServed <- LoginReqServed + 1
            printfn "In SendLogin: %s" userId
            let url = "akka.tcp://clientSystem@localhost:4000/user/" + userId
            let userRef = select url twitterSystem
            userRef <! ("ReceiveLoginAck",userId,loginStatus, new List<String>(), new List<String>())
            

        | SendLogout (userId) ->
            totalReqServed <- totalReqServed + 1
            LogoutReqServed <- LogoutReqServed + 1
            let url = "akka.tcp://clientSystem@localhost:4000/user/" + userId
            let userRef = select url twitterSystem
            userRef <! ("ReceiveLogoutAck",userId,"", new List<String>(), new List<String>())
            
        | PrintSenderInfo ->
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Total Outgoing Messages: %i \n" totalReqServed)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Login Requests Served: %i \n" LoginReqServed)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Logout Requests Served: %i \n" LogoutReqServed)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Outgoing Tweet: %i \n" TweetsServed)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Outgoing ReTweets: %i \n" ReTweetsServed)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "QueryByHashTagOrMentionRequest Requests Served: %i \n" QueriesReqServed)
            twitterSystem.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(6000.0),mailbox.Self, PrintSenderInfo)



        return! loop()
    }
    loop()



let TweetsActor  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    //let mutable tweetsMap = Map.empty  // store tweetid and tweet
    let tweetsMap = new Dictionary<int,string>()
    //let mutable tweetsUserMap = Map.empty  // store tweetid and user(author)
    let tweetsUserMap = new Dictionary<int, string>()
    let hashTagsTweetMap = new Dictionary<string, List<int>>()  // hashtag tweet ids  map for querying
    let mentionsTweetMap = new Dictionary<string, List<int>>()  // mentions tweet ids map for querying
    let mutable tweetId = 0;
    let rec loop() = actor{
        let! message = mailbox.Receive()
        
        match message with
        | Tweet (userId , tweet) ->
            tweetId <- tweetId + 1
            //tweetsMap <- tweetsMap.Add(userId, tweet)
            tweetsMap.Add(tweetId, tweet)
            //tweetsUserMap <- tweetsUserMap.Add(tweetId, userId)
            tweetsUserMap.Add(tweetId, userId)
            // get folllowers of userid
            let actorPath =  @"akka://twitterSystem/user/usersRef"
            let usersRef = select actorPath twitterSystem
            usersRef <! GetFollowers(userId, tweetId)
            let actorPath =  @"akka://twitterSystem/user/tweetsParserRef"
            let tweetsParserRef = select actorPath twitterSystem
            //usersRef <! GetFollowers(userId, tweetId)
            tweetsParserRef <! GetHashTagsAndMentions(userId,tweetId, tweet)

        | ReTweet (userId, tweetId) ->
            let tweetmsg = tweetsMap.[tweetId]
            let originUserId = tweetsUserMap.[tweetId]
            let actorPath =  @"akka://twitterSystem/user/usersRef"
            let usersRef = select actorPath twitterSystem
            usersRef <! GetFollowersForRetweet(userId, originUserId, tweetId)

            

        | ReceiveFollowers (userId, tweetId, followerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweetId, tweet, followerList)

        | ReceiveFollowersForRetweet (userId, tweetId, originUserId, followerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendRetweet(userId, originUserId, tweet, followerList, tweetId)

        | ReceiveMentions (userId, tweetId, mentionDict) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweetId, tweet, mentionDict)
            for  mention in mentionDict do
                //printfn "@@@@Parsed hashtag is %s" hashTag
                let actualmention =  "@" + mention.Key
                if mentionsTweetMap.ContainsKey(actualmention) then 
                    mentionsTweetMap.[actualmention].Add(tweetId)
                else
                    let listTweetId = new List<int>()
                    listTweetId.Add(tweetId)
                    mentionsTweetMap.Add(actualmention, listTweetId)
            

        | ReceiveHashTags(userId, tweetId, listHashTags) ->
            // Get followers of that hashtag
            let actorPath =  @"akka://twitterSystem/user/usersRef"
            let usersRef = select actorPath twitterSystem
            for hashTag in listHashTags do
                //printfn "@@@@Parsed hashtag is %s" hashTag
                if hashTagsTweetMap.ContainsKey(hashTag) then 
                    hashTagsTweetMap.[hashTag].Add(tweetId)
                else
                    let listTweetId = new List<int>()
                    listTweetId.Add(tweetId)
                    hashTagsTweetMap.Add(hashTag, listTweetId)
                usersRef <! GetFollowersOfHashTag (userId, tweetId, hashTag)
            printfn ""

        | ReceiveFollowersOfHashTag(userId, tweetId, hashTagFollowerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweetId, tweet, hashTagFollowerList)


        | QueryByHashTagOrMention (userid, tag) ->
            let listTweet = new List<String>()
            let tweetersList = new List<String>()
            printfn "@@Mentions tweet map %A" mentionsTweetMap
            if tag.[0] = '@' then
                if mentionsTweetMap.ContainsKey(tag) then
                    let listTweetId = mentionsTweetMap.[tag]
                    for tweetId in listTweetId do
                        listTweet.Add(tweetsMap.[tweetId])
                        tweetersList.Add(tweetsUserMap.[tweetId])
   
            else if tag.[0] = '#' then
                if hashTagsTweetMap.ContainsKey(tag) then
                    let listTweetId = hashTagsTweetMap.[tag]
                    for tweetId in listTweetId do
                        listTweet.Add(tweetsMap.[tweetId])
                        tweetersList.Add(tweetsUserMap.[tweetId])

            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! Query(listTweet, tweetersList, userid, tag)

        | _ -> 
            printfn "Wrong input"

        return! loop()
    }
    loop()



let TwitterServer  usersRef tweetsRef (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"

    let mutable totalReqReceived = 0
    // let mutable RegistrationRequest = 0
    let mutable LoginRequest = 0
    let mutable LogoutRequest = 0
    let mutable FollowRequest = 0
    let mutable FollowHashTagRequest = 0
    let mutable TweetRequest = 0
    let mutable ReTweetRequest = 0
    let mutable QueryByHashTagOrMentionRequest = 0
    let mutable RegistrationRequest = 0
    let StatsFilepath = "ServerLogs/EngineRequestsReceived.txt"
    let rec loop() = actor{
        let! (message:Object) = mailbox.Receive()
        

        // printfn "Message received from the other side %A" message
        let (instruction, arg1, arg2, arg3) : Tuple<String,string,string,string> = downcast message
        match instruction with
        | "Initiate" ->
            File.WriteAllText(StatsFilepath, "")
            twitterSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(6000.0),TimeSpan.FromMilliseconds(60000.0),mailbox.Self, ("PrintInfo", "","", "")) 
        | "RegisterAccount" ->
            totalReqReceived <- totalReqReceived + 1
            
            RegistrationRequest <- RegistrationRequest + 1
            File.AppendAllText(StatsFilepath, sprintf "%i \n" totalReqReceived)
            
            let userid = arg1
            let password = arg2
            printfn "%s %s" userid password
            usersRef <! RegisterAccount(userid, password)

        | "Login" ->
            totalReqReceived <- totalReqReceived + 1
            LoginRequest <- LoginRequest + 1
            let userid = arg1
            let password = arg2
            usersRef <! Login(userid, password)

        | "Logout" ->
            totalReqReceived <- totalReqReceived + 1
            LogoutRequest <- LogoutRequest + 1
            let userid = arg1;
            usersRef <! Logout(userid)

        | "Follow" ->
            totalReqReceived <- totalReqReceived + 1
            FollowRequest <- FollowRequest + 1
            let userId = arg1
            let userIdOfFollowed = arg2
            usersRef <! Follow(userId, userIdOfFollowed)

        | "FollowHashTag" ->
            totalReqReceived <- totalReqReceived + 1
            FollowHashTagRequest <- FollowHashTagRequest + 1
            let userId = arg1
            let hashTag = arg2
            usersRef <! FollowHashTag(userId, hashTag)

        | "Tweet" ->
            totalReqReceived <- totalReqReceived + 1
            TweetRequest <- TweetRequest + 1
            printfn "&&&&&&&&&&&&&&&%i \n" totalReqReceived
            let userId = arg1
            let tweet = arg2
            tweetsRef <! Tweet(userId, tweet)

        | "ReTweet" ->
            totalReqReceived <- totalReqReceived + 1
            ReTweetRequest <- ReTweetRequest + 1
            let userId = arg1
            //let originUserId = arg2
            let tweetId = (int)arg2
            printfn "IN REWEET @@@ tweetid is %i" tweetId
            tweetsRef <! ReTweet(userId, tweetId)

        | "QueryByHashTagOrMention" ->
            totalReqReceived <- totalReqReceived + 1
            QueryByHashTagOrMentionRequest <- QueryByHashTagOrMentionRequest + 1
            let userId = arg1
            let tag = arg2
            tweetsRef <! QueryByHashTagOrMention(userId, tag) 

        | "PrintInfo" ->
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Total Requests Received: %i \n" totalReqReceived)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Login Requests Received: %i \n" LoginRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Logout Requests Received: %i \n" LogoutRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Follow Requests Received: %i \n" FollowRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "FollowHashTag Requests Received: %i \n" FollowHashTagRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "Tweet Requests Received: %i \n" TweetRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "ReTweet Requests Received: %i \n" ReTweetRequest)
            File.AppendAllText(StatsFilepath, DateTime.Now.ToString() + ":" + sprintf "QueryByHashTagOrMentionRequest Requests Received: %i \n" QueryByHashTagOrMentionRequest)
            


        return! loop()
    }
    loop()

let UsersActor (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //let mutable userPasswordMap = Map.empty
    let userPasswordMap = new Dictionary<string,string>()
    let mutable activeUsersSet = Set.empty
    let userFollowerList = new Dictionary<string, Dictionary<string,string>>()
    let hashTagFollowerList = new Dictionary<string, Dictionary<string, string>>()
    let rec loop() = actor {
        let! message = mailbox.Receive()
       
        let sender = mailbox.Sender()
       
       

        match message with 
        | RegisterAccount (userid, password) ->
            printfn "%s %s" userid password
            // Hash the password later
            let followerDict = new Dictionary<string,string>() 
            //userFollowerList <- userFollowerList.Add(userid, followerList)
            userFollowerList.Add(userid, followerDict)
            userPasswordMap.Add(userid, hashFunction password)
            //userPasswordMap <- userPasswordMap.Add(userid, password)
            


        | Login (userid, password) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            if userPasswordMap.ContainsKey(userid) then
                if (hashFunction password = userPasswordMap.[userid]) then
                    printfn "Welcome %s!! Login Successful"  userid
                    activeUsersSet <- activeUsersSet.Add(userid)
                    printfn "Later Welcome %s!! Login Successful"  userid
                    tweetsSenderRef <! SendLoginToken(userid, "success")


                    // send a token without which user cannot do further actions
                else
                    tweetsSenderRef <! SendLoginToken(userid, "failure")
                    printfn "Wrong password"
            else
                tweetsSenderRef <! SendLoginToken(userid, "failure")
                printfn "Username doesn't exist"
        


        | Logout userid ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            activeUsersSet <- activeUsersSet.Remove(userid)
            printfn "################################################################################################################################%A" activeUsersSet.Count
            tweetsSenderRef <! SendLogout(userid)

            // destroy token at the client side

        


        | Follow (userId, userIdOfFollowed) ->
            if userFollowerList.ContainsKey(userId) then
                let followerDict = userFollowerList.[userIdOfFollowed]
                if  not <| followerDict.ContainsKey(userId) then
                    followerDict.Add(userId, userId);

        


        | FollowHashTag(userId, hashTag) ->
            if hashTagFollowerList.ContainsKey(hashTag) then
                let mutable followerDict = hashTagFollowerList.[hashTag]
                if not <| (followerDict.ContainsKey(userId)) then
                    followerDict.Add(userId, userId)
            else 
                let mutable followerDict = new Dictionary<String, String>()
                followerDict.Add(userId, userId)
                hashTagFollowerList.Add(hashTag, followerDict)
            // let mutable followerList = hashTagFollowerList.[hashTag]
            // if followerList = null then
            //     followerList <-  new List<String>();
            //     followerList.Add(userId)
            //     hashTagFollowerList.Add(hashTag, followerList)
            // else 
            //     followerList.Add(userId)

        | GetFollowers (userId, tweetId) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            if userFollowerList.ContainsKey(userId) then
                let followerDict = userFollowerList.[userId]
                tweetsRef <! ReceiveFollowers(userId, tweetId, followerDict)
            // let followerList = new List<String>();
            // for entry in followerDict do
            //     followerList.Add(entry.Key)

                



        | GetFollowersForRetweet (userId, originUserId, tweetId) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            let followerDict = userFollowerList.[userId]
            // let followerList = new List<String>();
            // for entry in followerDict do
            //     followerList.Add(entry.Key)
            tweetsRef <! ReceiveFollowersForRetweet(userId, tweetId, originUserId, followerDict)


        | GetFollowersOfHashTag (userId, tweetId, hashTag) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            if (hashTagFollowerList.ContainsKey(hashTag)) then
                let followerDict = hashTagFollowerList.[hashTag]
                // let followerList = new List<String>();
                // for entry in followerDict do
                //     followerList.Add(entry.Key)
                tweetsRef <! ReceiveFollowersOfHashTag(userId, tweetId, followerDict)
            else 
                // let mutable followerList = new List<String>()
                let followerDict = new Dictionary<string, string>()
                // followerList.Add(userId)
                hashTagFollowerList.Add(hashTag, followerDict)

            
            





        | PrintInfo ->
            printfn "Active users are %A" activeUsersSet
            printfn "All users are %A" userPasswordMap
            printfn "User followers lists are %A" userFollowerList
            printfn "%A" mailbox.Self.Path

        | Test ->
            printfn "test"

        | _-> 
            printfn "Wrong input"
            


        return! loop()
    }
    loop()





[<EntryPoint>]
let main argv =
    let nodes = (int) argv.[0];
    let twitterSystem = ActorSystem.Create("twitterSystem", config);
    
    let tweetsSenderRef = spawn twitterSystem "tweetsSenderRef" (TweetsSenderActor twitterSystem);
    let usersRef = spawn twitterSystem "usersRef" (UsersActor twitterSystem);
    let tweetsRef = spawn twitterSystem "tweetsRef" (TweetsActor twitterSystem);
    let tweetsParserRef = spawn twitterSystem "tweetsParserRef" (TweetsParserActor twitterSystem);

    let twitterServerRef = spawn twitterSystem "twitterServerRef" (TwitterServer usersRef tweetsRef twitterSystem ) ;
    twitterServerRef <! ("Initiate","","","")
    tweetsSenderRef <! PrintSenderInfo
    // usersRef <! RegisterAccount("prasad", "prasad")
    // usersRef <! RegisterAccount("vaishnavi", "vaishnavi")
    // usersRef <! RegisterAccount("siddhi", "siddhi")
    // usersRef <! RegisterAccount("nikhil", "nikhil")
    // usersRef <! RegisterAccount("arijit", "arijit")
    // Thread.Sleep(1000)
    // usersRef <! Login("prasad", "prasad")
    // usersRef <! Login("vaishnavi", "vaishnavi")
    // usersRef <! Login("siddhi", "siddhi")
    // usersRef <! Login("nikhil", "nikhil")
    // usersRef <! Login("arijit", "arijit")
    // Thread.Sleep(1000)
    // usersRef <! Follow("vaishnavi", "prasad")
    // usersRef <! Follow("siddhi", "prasad")
    // usersRef <! Follow ("arijit", "vaishnavi")
    // usersRef <! Follow ("siddhi", "vaishnavi")
    // Thread.Sleep(1000)
    // usersRef <! FollowHashTag("nikhil", "#Mumbai")
    // usersRef <! FollowHashTag("prasad", "#Cricket")
    // Thread.Sleep(1000)
    // tweetsRef <! Tweet("prasad", "Hello followers...gooooood Morning. WITHOUT HASHTAGS")
    // tweetsRef <! Tweet("prasad", "Hello followers...gooooood Morning #Mumbai #India")
    // Thread.Sleep(1000)
    // tweetsRef <! Tweet("nikhil", "#Cricket is back #ipl @siddhi" )
    // Thread.Sleep(1000)
    // tweetsRef <! QueryByHashTagOrMention("arijit", "#Cricket")
    // Thread.Sleep(1000)
    // tweetsRef <! QueryByHashTagOrMention("vaishnavi", "@siddhi")
    // Thread.Sleep(1000)
    // tweetsRef <! ReTweet("vaishnavi", "nikhil", 3);
    //usersRef <! Logout("prasad")
    //usersRef <! PrintInfo
    System.Console.ReadKey() |> ignore
    0