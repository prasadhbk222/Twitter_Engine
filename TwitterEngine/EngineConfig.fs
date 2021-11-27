module Myconfig

open Akka.FSharp
open Akka.Remote
open System

let config =
    Configuration.parse
        @"akka {
            log-config-on-start = on
            stdout-loglevel = ERROR
            loglevel = ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote {
                helios.tcp {
                    transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                    applied-adapters = []
                    transport-protocol = tcp
                    port = 8080
                    hostname = localhost
                }
            }
        }"