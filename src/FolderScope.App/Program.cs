namespace FolderScope;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
        {
            var code = Headless.SelfTest();
            if (args.Length > 1)
            {
                try
                {
                    File.WriteAllText(args[1], (code == 0 ? "ok\n" : "fail " + code + "\n") + Headless.LastSelfTestDetail + "\n");
                }
                catch
                {
                    // The exit code is the result that matters.
                }
            }

            Environment.Exit(code);
        }

        if (args.Length > 0 && string.Equals(args[0], "--scan", StringComparison.OrdinalIgnoreCase))
            Environment.Exit(args.Length < 2 ? 2 : Headless.Scan(args[1]));

        ApplicationConfiguration.Initialize();
        var demo = false;
        string? shot = null;
        Size? client = null;
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--demo", StringComparison.OrdinalIgnoreCase))
                demo = true;
            else if (string.Equals(args[i], "--shot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                shot = args[++i];
            else if (string.Equals(args[i], "--client", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && TrySize(args[++i], out var parsed))
                client = parsed;
            else
                rest.Add(args[i]);
        }

        Application.Run(new MainForm(rest.ToArray(), demo, shot, client));
    }

    private static bool TrySize(string text, out Size size)
    {
        size = Size.Empty;
        var parts = text.Split('x', 'X');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
            return false;
        size = new Size(width, height);
        return width > 0 && height > 0;
    }
}
