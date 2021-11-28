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
    | ReceiveFollowers of string*int*Dictionary<string,string> //userid/ //tweetid //listoffollowers
    | ReceiveFollowersForRetweet of string*int*string*Dictionary<string,string>  //userid //tweetid //originUserid // followerlist
    | ReceiveFollowersOfHashTag of string*int*Dictionary<string,string> //userid/ //tweetid //listofhashtagfollowers
    | ReceiveHashTags of string*int*List<string>  //userid //tweetid //Listoffollowers
    | ReceiveMentions of string*int*Dictionary<string,string>  //userid //tweetid //Listoffollowers
    | QueryByHashTagOrMention of string*string   //userid // hashtag or mention


//@@@@@@@@@@@@@@@ write code to query tweets based on hashtags and mentions

type TweetsSenderActorInstruction=
    | SendTweet of string*string*Dictionary<string,string> // tweet and recipientList
    | Query of List<String>*List<string>*string            // tweet list, tweeter list and userid 
    | SendRetweet of string*string*string*Dictionary<string,string>  //(userId, originUserId, tweet, followerList)

type TweetParser =
    | GetHashTagsAndMentions of string*int*string //tweet

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
    
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | SendTweet (userid,tweet, recipientDict) ->
            for recipient in recipientDict do
                let url = "akka.tcp://clientSystem@localhost:4000/user/" + recipient.Key
                let userRef = select url twitterSystem
                userRef <! ("ReceiveTweet", sprintf "The tweet by %s =>%s<= was sent to %s" userid tweet recipient.Key , "", new List<String>(), new List<String>())
                printfn "The tweet by %s =>%s<= was sent to %s" userid tweet recipient.Key

            //recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet by %s =>%s<= was sent to %s"  index userid tweet item)
            // recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet =>%s<= was sent to %s" index tweet recipientList.[index])

        | Query(tweetList, tweeterList, user) ->
            // mapping of tweetlist and tweeter list will be done at client according to index
            let tc = tweetList.Count - 1 
            for i in 0..tc do
                printfn "Following tweets were sent to %s : %s : %s"  user tweeterList.[i] tweetList.[i]

        | SendRetweet (userId, originUserId, tweet, recipientDict) ->
            for recipient in recipientDict do
                printfn "The tweet by %s was retweeted by %s =>%s<= was sent to %s" originUserId  userId tweet recipient.Key

        

        


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

        | ReTweet (userId, originUserId, tweetId) ->
            let tweetmsg = tweetsMap.[tweetId]
            let actorPath =  @"akka://twitterSystem/user/usersRef"
            let usersRef = select actorPath twitterSystem
            usersRef <! GetFollowersForRetweet(userId, originUserId, tweetId)

            

        | ReceiveFollowers (userId, tweetId, followerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweet, followerList)

        | ReceiveFollowersForRetweet (userId, tweetId, originUserId, followerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendRetweet(userId, originUserId, tweet, followerList)

        | ReceiveMentions (userId, tweetId, mentionDict) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweet, mentionDict)
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
            tweetsSenderRef <! SendTweet(userId, tweet, hashTagFollowerList)


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
            tweetsSenderRef <! Query(listTweet, tweetersList, userid)

            


        | _-> 
            printfn "Wrong input"



        return! loop()
    }
    loop()



let TwitterServer  usersRef tweetsRef (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    let rec loop() = actor{
        let! (message:Object) = mailbox.Receive()
        // printfn "Message received from the other side %A" message
        let (instruction, arg1, arg2, arg3) : Tuple<String,string,string,string> = downcast message
        match instruction with 
        | "RegisterAccount" ->
            let userid = arg1
            let password = arg2
            printfn "%s %s" userid password
            usersRef <! RegisterAccount(userid, password)

        | "Login" ->
            let userid = arg1
            let password = arg2
            usersRef <! Login(userid, password)

        | "Logout" ->
            let userid = arg1;
            usersRef <! Logout(userid)

        | "Follow" ->
            let userId = arg1
            let userIdOfFollowed = arg2
            usersRef <! Follow(userId, userIdOfFollowed)

        | "FollowHashTag" ->
            let userId = arg1
            let hashTag = arg2
            usersRef <! FollowHashTag(userId, hashTag)

        | "Tweet" ->
            let userId = arg1
            let tweet = arg2
            tweetsRef <! Tweet(userId, tweet)

        | "ReTweet" ->
            let userId = arg1
            let originUserId = arg2
            let tweetId = (int)arg3
            tweetsRef <! ReTweet(userId, originUserId, tweetId)

        | "QueryByHashTagOrMention" ->
            let userId = arg1
            let tag = arg2
            tweetsRef <! QueryByHashTagOrMention(userId, tag) 

        

        
             

        


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
            userPasswordMap.Add(userid, password)
            //userPasswordMap <- userPasswordMap.Add(userid, password)
            


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

        


        | Follow (userId, userIdOfFollowed) ->
            if userFollowerList.ContainsKey(userId) then
                let followerDict = userFollowerList.[userIdOfFollowed]
                if  not <| followerDict.ContainsKey(userId) then
                    followerDict.Add(userId, userId);

        


        | FollowHashTag(userId, hashTag) ->
            if hashTagFollowerList.ContainsKey(hashTag) then
                let mutable followerDict = hashTagFollowerList.[hashTag]
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