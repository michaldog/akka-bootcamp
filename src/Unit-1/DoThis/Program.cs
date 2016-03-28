﻿namespace WinTail
{
    using Akka.Actor;

    class Program
    {
        public static ActorSystem MyActorSystem;

        static void Main(string[] args)
        {
            // initialize MyActorSystem
            MyActorSystem = ActorSystem.Create("MyActorSystem");

            var consoleWriterActorProps = Props.Create<ConsoleWriterActor>();
            var consoleWriterActor = MyActorSystem.ActorOf(consoleWriterActorProps, "consoleWriterActor");

            var validationActorProps = Props.Create(() => new ValidationActor(consoleWriterActor));
            var validationActor = MyActorSystem.ActorOf(validationActorProps, "validationActor");

            var consoleReaderActorProps = Props.Create(() => new ConsoleReaderActor(validationActor));
            var consoleReadActor = MyActorSystem.ActorOf(consoleReaderActorProps, "consoleReaderActor");

            // tell console reader to begin
            consoleReadActor.Tell(ConsoleReaderActor.StartCommand);

            // blocks the main thread from exiting until the actor system is shut down
            MyActorSystem.AwaitTermination();
        }
    }
}
