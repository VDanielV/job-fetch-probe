using JobFetchProbe.Console;

// No arguments: interactive prompt. With arguments: run one command and exit (scripts, CI).
return args.Length == 0
    ? await new InteractiveShell(Console.In, Console.Out, Console.Error).RunAsync()
    : await ProbeApp.RunAsync(args, Console.Out, Console.Error);
