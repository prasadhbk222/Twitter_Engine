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

type UsersActorInstructions=
    | RegisterAccount of string*string
    | Test
    | Login of string*string
    | Logout of string
    | PrintInfo
    | GetFollowers of string*int
    | Follow of string*string //userid // useridoffollowed
    | FollowHashTag of string*string //userid //hashtag
    | GetFollowersOfHashTag of string*int*string //userid //tweetid//hashtag 
 
type TweetsActorInstructions=
    | Tweet of string*string   //userid //tweet
    | ReTweet of string*string*string //userid //copyfromuserid //tweetid
    | ReceiveFollowers of string*int*List<string> //userid/ //tweetid //listoffollowers
    | ReceiveFollowersOfHashTag of string*int*List<string> //userid/ //tweetid //listofhashtagfollowers
    | ReceiveHashTags of string*int*List<string>  //userid //tweetid //Listoffollowers
    | ReceiveMentions of string*int*List<string>  //userid //tweetid //Listoffollowers


// write code to handle received hashtags and mentions


type TweetsSenderActorInstruction=
    | SendTweet of string*string*List<String> // tweet and recipientList

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
            let listMentions = new List<string>()
            for word in words do
                if word.[0] = '#' then
                    listHashTags.Add(word)
                if word.[0] = '@' then
                    listMentions.Add(word.Substring(1))
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            tweetsRef <! ReceiveHashTags(userId, tweetId, listHashTags)
            tweetsRef <! ReceiveMentions(userId, tweetId, listMentions)




        


        return! loop()
    }
    loop()

let TweetsSenderActor  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    let rec loop() = actor{
        let! message = mailbox.Receive()
        match message with
        | SendTweet (userid,tweet, recipientList) ->
            recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet by %s =>%s<= was sent to %s"  index userid tweet item)
            // recipientList |> Seq.iteri (fun index item -> printfn "%i: The tweet =>%s<= was sent to %s" index tweet recipientList.[index])

        


        return! loop()
    }
    loop()



let TweetsActor  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    //let mutable tweetsMap = Map.empty  // store tweetid and tweet
    let tweetsMap = new Dictionary<int,string>()
    //let mutable tweetsUserMap = Map.empty  // store tweetid and user(author)
    let tweetsUserMap = new Dictionary<int, string>()
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
            

        | ReceiveFollowers (userId, tweetId, followerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweet, followerList)

        | ReceiveMentions (userId, tweetId, mentionList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweet, mentionList)

        | ReceiveHashTags(userId, tweetId, listHashTags) ->
            // Get followers of that hashtag
            let actorPath =  @"akka://twitterSystem/user/usersRef"
            let usersRef = select actorPath twitterSystem
            for hashTag in listHashTags do
                //printfn "@@@@Parsed hashtag is %s" hashTag
                usersRef <! GetFollowersOfHashTag (userId, tweetId, hashTag)
            printfn ""

        | ReceiveFollowersOfHashTag(userId, tweetId, hashTagFollowerList) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsSenderRef"
            let tweetsSenderRef = select actorPath twitterSystem
            let tweet = tweetsMap.[tweetId]
            tweetsSenderRef <! SendTweet(userId, tweet, hashTagFollowerList)


        | ReceiveMentions(userId, tweetId, listMentions) ->
            printfn ""


        | _-> 
            printfn "Wrong input"



        return! loop()
    }
    loop()



let TwitterServer  (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //printfn "abc"
    let rec loop() = actor{
        let! message = mailbox.Receive()

        


        return! loop()
    }
    loop()

let UsersActor (twitterSystem : ActorSystem) (mailbox: Actor<_>) =
    //let mutable userPasswordMap = Map.empty
    let userPasswordMap = new Dictionary<string,string>()
    let mutable activeUsersSet = Set.empty
    let userFollowerList = new Dictionary<string, List<string>>()
    let hashTagFollowerList = new Dictionary<string, List<string>>()
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()

        match message with 
        | RegisterAccount (userid, password) ->
            printfn "%s %s" userid password
            // Hash the password later
            let followerList = new List<string>() 
            //userFollowerList <- userFollowerList.Add(userid, followerList)
            userFollowerList.Add(userid, followerList)
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
            let followerList = userFollowerList.[userIdOfFollowed]
            followerList.Add(userId)

        | FollowHashTag(userId, hashTag) ->
            if hashTagFollowerList.ContainsKey(hashTag) then
                let mutable followerList = hashTagFollowerList.[hashTag]
                followerList.Add(userId)
            else 
                let mutable followerList = new List<String>()
                followerList.Add(userId)
                hashTagFollowerList.Add(hashTag, followerList)
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
            tweetsRef <! ReceiveFollowers(userId, tweetId, userFollowerList.[userId])

        | GetFollowersOfHashTag (userId, tweetId, hashTag) ->
            let actorPath =  @"akka://twitterSystem/user/tweetsRef"
            let tweetsRef = select actorPath twitterSystem
            if (hashTagFollowerList.ContainsKey(hashTag)) then
                tweetsRef <! ReceiveFollowersOfHashTag(userId, tweetId, hashTagFollowerList.[hashTag])
            else 
                let mutable followerList = new List<String>()
                // followerList.Add(userId)
                hashTagFollowerList.Add(hashTag, followerList)

            
            





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
    let twitterSystem = ActorSystem.Create("twitterSystem");
    let twitterServerRef = spawn twitterSystem "twitterServerRef" (TwitterServer twitterSystem);
    let tweetsSenderRef = spawn twitterSystem "tweetsSenderRef" (TweetsSenderActor twitterSystem);
    let usersRef = spawn twitterSystem "usersRef" (UsersActor twitterSystem);
    let tweetsRef = spawn twitterSystem "tweetsRef" (TweetsActor twitterSystem);
    let tweetsParserRef = spawn twitterSystem "tweetsParserRef" (TweetsParserActor twitterSystem);

    usersRef <! RegisterAccount("prasad", "prasad")
    usersRef <! RegisterAccount("vaishnavi", "vaishnavi")
    usersRef <! RegisterAccount("siddhi", "siddhi")
    usersRef <! RegisterAccount("nikhil", "nikhil")
    Thread.Sleep(1000)
    usersRef <! Login("prasad", "prasad")
    usersRef <! Login("vaishnavi", "vaishnavi")
    usersRef <! Login("siddhi", "siddhi")
    usersRef <! Login("nikhil", "nikhil")
    Thread.Sleep(1000)
    usersRef <! Follow("vaishnavi", "prasad")
    usersRef <! Follow("siddhi", "prasad")
    Thread.Sleep(1000)
    usersRef <! FollowHashTag("nikhil", "#Mumbai")
    usersRef <! FollowHashTag("prasad", "#Cricket")
    Thread.Sleep(1000)
    tweetsRef <! Tweet("prasad", "Hello followers...gooooood Morning. WITHOUT HASHTAGS")
    tweetsRef <! Tweet("prasad", "Hello followers...gooooood Morning #Mumbai #India")
    tweetsRef <! Tweet("nikhil", "#Cricket is back #ipl @siddhi" )
    //usersRef <! Logout("prasad")
    //usersRef <! PrintInfo
    System.Console.ReadKey() |> ignore
    0