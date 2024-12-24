namespace Spectre.Console.Prompts;

public class PromptAbortException : Exception
{
    internal PromptAbortException(string message)
        : base(message)
    {
    }
}
