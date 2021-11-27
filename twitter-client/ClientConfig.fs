module Myconfig

open Akka.FSharp
open Akka.Remote
open System

let config =
    Configuration.parse
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
		            applied-adapters = []
		            transport-protocol = tcp
		            port = 0
		            hostname = localhost
                }
            }
        }"