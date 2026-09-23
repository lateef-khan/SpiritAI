using System.Text;

namespace SpiritAI.Chatwoot;

/// <summary>Reads an openui-lang program into its statements, for <see cref="OpenUiText"/>. Throws <see cref="FormatException"/> on what it cannot read.</summary>
internal sealed class OpenUiReader(string source)
{
    private int _at;

    public Dictionary<string, OpenUiValue> Statements()
    {
        var statements = new Dictionary<string, OpenUiValue>(StringComparer.Ordinal);

        while (SkipSpace())
        {
            var name = Identifier() ?? throw new FormatException("A statement starts with a name.");
            Expect('=');
            statements[name] = Parse();
        }

        return statements;
    }

    private OpenUiValue Parse()
    {
        SkipSpace();

        var c = Peek();

        if (c == '"')
        {
            return new OpenUiValue.Text(Text());
        }

        if (c == '[')
        {
            _at++;
            return new OpenUiValue.List(Items(']'));
        }

        if (c == '{')
        {
            _at++;
            return new OpenUiValue.List(ObjectValues());
        }

        if (Identifier() is { } id)
        {
            SkipSpace();

            if (Peek() == '(')
            {
                _at++;
                return new OpenUiValue.Call(Items(')'));
            }

            return new OpenUiValue.Name(id);
        }

        // A number, $binding, @step, or anything else: no words in it.
        while (_at < source.Length && !",)]}\n".Contains(source[_at], StringComparison.Ordinal))
        {
            _at++;
        }

        return new OpenUiValue.Other();
    }

    private List<OpenUiValue> Items(char close)
    {
        var items = new List<OpenUiValue>();

        SkipSpace();

        if (Peek() == close)
        {
            _at++;
            return items;
        }

        while (true)
        {
            items.Add(Parse());
            SkipSpace();

            var c = Next();

            if (c == close)
            {
                return items;
            }

            if (c != ',')
            {
                throw new FormatException($"Expected ',' or '{close}'.");
            }
        }
    }

    private List<OpenUiValue> ObjectValues()
    {
        var values = new List<OpenUiValue>();

        SkipSpace();

        if (Peek() == '}')
        {
            _at++;
            return values;
        }

        while (true)
        {
            SkipSpace();

            if (Peek() == '"')
            {
                Text();
            }
            else if (Identifier() is null)
            {
                throw new FormatException("An object key is a name or a string.");
            }

            Expect(':');
            values.Add(Parse());
            SkipSpace();

            var c = Next();

            if (c == '}')
            {
                return values;
            }

            if (c != ',')
            {
                throw new FormatException("Expected ',' or '}'.");
            }
        }
    }

    private string Text()
    {
        _at++;
        var text = new StringBuilder();

        while (true)
        {
            var c = Next();

            if (c == '"')
            {
                return text.ToString();
            }

            if (c != '\\')
            {
                text.Append(c);
                continue;
            }

            var escaped = Next();
            text.Append(escaped switch
            {
                'n' => '\n',
                't' => '\t',
                _ => escaped,
            });
        }
    }

    private string? Identifier()
    {
        var start = _at;

        while (_at < source.Length && (char.IsLetterOrDigit(source[_at]) || source[_at] == '_'))
        {
            _at++;
        }

        return _at > start && !char.IsDigit(source[start]) ? source[start.._at] : Reset(start);
    }

    private string? Reset(int start)
    {
        _at = start;
        return null;
    }

    private void Expect(char expected)
    {
        SkipSpace();

        if (Next() != expected)
        {
            throw new FormatException($"Expected '{expected}'.");
        }
    }

    /// <returns>Whether anything is left.</returns>
    private bool SkipSpace()
    {
        while (_at < source.Length && char.IsWhiteSpace(source[_at]))
        {
            _at++;
        }

        return _at < source.Length;
    }

    private char Peek() => _at < source.Length ? source[_at] : '\0';

    private char Next() => _at < source.Length ? source[_at++] : throw new FormatException("The program ends early.");
}
