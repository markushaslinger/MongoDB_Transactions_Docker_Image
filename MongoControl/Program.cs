using System.Text;
using CliWrap;

const string ConfigFilePath = "/etc/mongod.conf.orig";
const string DataDir = "/var/lib/mongodb";
const string Header = """

___  ___                       ____________            _ _   _       _____                              _   _                 
|  \/  |                       |  _  | ___ \          (_| | | |     |_   _|                            | | (_)                
| .  . | ___  _ __   __ _  ___ | | | | |_/ / __      ___| |_| |__     | |_ __ __ _ _ __  ___  __ _  ___| |_ _  ___  _ __  ___ 
| |\/| |/ _ \| '_ \ / _` |/ _ \| | | | ___ \ \ \ /\ / | | __| '_ \    | | '__/ _` | '_ \/ __|/ _` |/ __| __| |/ _ \| '_ \/ __|
| |  | | (_) | | | | (_| | (_) | |/ /| |_/ /  \ V  V /| | |_| | | |   | | | | (_| | | | \__ | (_| | (__| |_| | (_) | | | \__ \
\_|  |_/\___/|_| |_|\__, |\___/|___/ \____/    \_/\_/ |_|\__|_| |_|   \_|_|  \__,_|_| |_|___/\__,_|\___|\__|_|\___/|_| |_|___/
                     __/ |                                                                                                    
                    |___/                                                                                                     

""";

Console.WriteLine(Header);
PrintBlock("Attempting to start MongoDB in a single replica mode with transactions enabled...");

PrintBlock("Step 1: patching Mongo config file to allow replication set rs0");
await PatchMongoConfigFile();

PrintBlock("Step 2: Attempting to stop any running mongod processes, this may fail if none are running");
await RunCommand("mongosh", """--eval "use admin;" """);
await Task.Delay(TimeSpan.FromSeconds(1));
await RunCommand("mongosh", """--eval "db.shutdownServer();" """);
await Task.Delay(TimeSpan.FromSeconds(2));

PrintBlock($"Step 3: ensuring data directory {DataDir}");
Directory.CreateDirectory(DataDir);

PrintBlock("Step 4: Starting forked mongod process");
await RunCommand("""/bin/mongod""", $"--bind_ip_all --fork --logpath /mongo/log --config {ConfigFilePath}");

PrintBlock("Step 5: Waiting for DB process to come alive");
await Task.Delay(TimeSpan.FromSeconds(4));

PrintBlock("Step 6: Initializing replication set");
await RunCommand("mongosh", """--eval "rs.initiate();" """);

PrintBlock("Done, DB should now accept connections and allow transactions");

while (true)
{
    await Task.Delay(TimeSpan.FromMinutes(1));
    Console.WriteLine($"Still here {DateTime.Now}");
}

static void PrintBlock(string text)
{
    var border = new string('=', text.Length);
    
    Console.WriteLine(border);
    Console.WriteLine(text);
    Console.WriteLine(border);
    Console.WriteLine();
}

async Task PatchMongoConfigFile()
{
    var allText = await File.ReadAllTextAsync(ConfigFilePath);
    allText = allText.Replace("#replication:", 
        $$"""replication:{{Environment.NewLine}}  replSetName: "rs0" """);
    await File.WriteAllTextAsync(ConfigFilePath, allText);
}

async Task RunCommand(string cmd, string args)
{
    var outSink = new StringBuilder();
    try
    {
        await Cli.Wrap(cmd)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(outSink))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(outSink))
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
    }
    catch (Exception ex)
    {
        outSink.Append(ex);
    }

    var outLines = outSink.ToString().Split(Environment.NewLine)
        .Select(l => $"{new string(' ', 2)}> {l}");
    Console.WriteLine(string.Join(Environment.NewLine, outLines));
}