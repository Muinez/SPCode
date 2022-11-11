﻿using System;
using System.Collections.Generic;
using System.Text;
using SourcepawnCondenser.SourcemodDefinition;
using SourcepawnCondenser.Tokenizer;

namespace SourcepawnCondenser;

public partial class Condenser
{
    private static readonly char[] CommentTrimSymbols = { '/', '*', ' ', '\t' };
    private static readonly char[] FullNameTrimSymbols = { ' ', '\t' };

    private readonly SMDefinition _def;

    private readonly string _fileName;
    private readonly int _length;
    private readonly string _source;

    private readonly Token[] _tokens;
    private int _position;

    public Condenser(string sourceCode, string fileName)
    {
        _tokens = Tokenizer.Tokenizer.TokenizeString(sourceCode, true).ToArray();
        _position = 0;
        _length = _tokens.Length;
        _def = new SMDefinition();
        _source = sourceCode;
        if (fileName.EndsWith(".inc", StringComparison.InvariantCultureIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - 4);
        }

        _fileName = fileName;
    }

    public SMDefinition Condense()
    {
        Token ct;
        while ((ct = _tokens[_position]).Kind != TokenKind.EOF)
        {
            int index;
            switch (ct.Kind)
            {
                case TokenKind.FunctionIndicator:
                    index = ConsumeSMFunction();
                    break;
                case TokenKind.EnumStruct:
                    index = ConsumeSMEnumStruct();
                    break;
                case TokenKind.Enum:
                    index = ConsumeSMEnum();
                    break;
                case TokenKind.Struct:
                    index = ConsumeSMStruct();
                    break;
                case TokenKind.PreprocessorDirective:
                    index = ConsumeSMPPDirective();
                    break;
                case TokenKind.Constant:
                    index = ConsumeSMConstant();
                    break;
                case TokenKind.MethodMap:
                    index = ConsumeSMMethodmap();
                    break;
                case TokenKind.TypeSet:
                    index = ConsumeSMTypeset();
                    break;
                case TokenKind.TypeDef:
                    index = ConsumeSMTypedef();
                    break;
                case TokenKind.Identifier:
                    index = ConsumeSMIdentifier();
                    break;
                default:
                    index = -1;
                    break;
            }

            if (index != -1)
            {
                _position = index + 1;
                continue;
            }

            ++_position;
        }

        _def.Sort();
        return _def;
    }

    private int BacktraceTestForToken(int startPosition, TokenKind testKind, bool ignoreEol, bool ignoreOtherTokens)
    {
        for (var i = startPosition; i >= 0; --i)
        {
            if (_tokens[i].Kind == testKind)
            {
                return i;
            }

            if (ignoreOtherTokens)
            {
                continue;
            }

            if (_tokens[i].Kind == TokenKind.EOL && ignoreEol)
            {
                continue;
            }

            return -1;
        }

        return -1;
    }

    private int FortraceTestForToken(int startPosition, TokenKind testKind, bool ignoreEol, bool ignoreOtherTokens)
    {
        for (var i = startPosition; i < _length; ++i)
        {
            if (_tokens[i].Kind == testKind)
            {
                return i;
            }

            if (ignoreOtherTokens)
            {
                continue;
            }

            if (_tokens[i].Kind == TokenKind.EOL && ignoreEol)
            {
                continue;
            }

            return -1;
        }

        return -1;
    }

    private static string TrimComments(ReadOnlySpan<char> comment)
    {
        var outString = new StringBuilder();
        var lines = comment.SplitLines();

        int i = 0;
        foreach (var lineSplitEntry in lines)
        {
            var line = lineSplitEntry.Line.Trim().TrimStart(CommentTrimSymbols);
            if (!line.IsWhiteSpace())
            {
                if (i > 0)
                {
                    outString.AppendLine();
                }

                if (line.StartsWith("@param"))
                {
                    outString.Append(FormatParamLineString(line.ToString()).Trim());
                }
                else
                {
                    outString.Append(line);
                }
            }

            i++;
        }

        return outString.ToString();
    }

    private static string TrimFullname(ReadOnlySpan<char> name)
    {
        var outString = new StringBuilder();
        int i = 0;
        foreach (var lineEntry in name.SplitLines())
        {
            var line = lineEntry.Line;
            if (!line.IsWhiteSpace())
            {
                if (i > 0)
                {
                    outString.Append(' ');
                }

                outString.Append(line.Trim(FullNameTrimSymbols));
            }

            i++;
        }

        return outString.ToString();
    }


    private static string FormatParamLineString(string line)
    {
        var split = line.Split(new[] { ' ' }, 3);
        if (split.Length > 2)
        {
            return ("@param " + split[1]).PadRight(24, ' ') + " " + split[2].Trim(' ', '\t');
        }

        return line;
    }

    private int ConsumeSMIdentifier()
    {
        var index = ConsumeSMVariable();
        return index == -1 ? ConsumeSMFunction() : index;
    }
}