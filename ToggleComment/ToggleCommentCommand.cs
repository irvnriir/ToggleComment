﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ToggleComment.Codes;
using ToggleComment.Utils;

namespace ToggleComment
{
    /// <summary>
    /// 選択された行のコメントアウト・解除を行うコマンドです。
    /// </summary>
    internal sealed class ToggleCommentCommand : CommandBase
    {
        /// <summary>
        /// コマンドの実行を委譲するインスタンスです。
        /// </summary>
        private readonly IOleCommandTarget _commandTarget;

        /// <summary>
        /// コマンドのIDです。
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// コメントのパターンです。
        /// </summary>
        private readonly IDictionary<string, ICodeCommentPattern[]> _patterns = new Dictionary<string, ICodeCommentPattern[]>();

        /// <summary>
        /// コマンドメニューグループのIDです。
        /// </summary>
        public static readonly Guid CommandSet = new Guid("85542055-97d7-4219-a793-8c077b81b25b");

        /// <summary>
        /// シングルトンのインスタンスを取得します。
        /// </summary>
        public static ToggleCommentCommand Instance { get; private set; }

        /// <summary>
        /// インスタンスを初期化します。
        /// </summary>
        /// <param name="package">コマンドを提供するパッケージ</param>
        private ToggleCommentCommand(Package package) : base(package, CommandId, CommandSet)
        {
            _commandTarget = (IOleCommandTarget)ServiceProvider.GetService(typeof(SUIHostCommandDispatcher));
        }

        /// <summary>
        /// このコマンドのシングルトンのインスタンスを初期化します。
        /// </summary>
        /// <param name="package">コマンドを提供するパッケージ</param>
        public static void Initialize(Package package)
        {
            Instance = new ToggleCommentCommand(package);
        }

        /// <inheritdoc />
        protected override void Execute(object sender, EventArgs e)
        {
            var dte = (DTE2)ServiceProvider.GetService(typeof(DTE));
            if (dte?.ActiveDocument.Object("TextDocument") is TextDocument textDocument)
            {
                var patterns = _patterns.GetOrAdd(textDocument.Language, CreateCommentPatterns);
                if (0 < patterns.Length)
                {
                    var text = GetTextOfSelectedLines(textDocument.Selection);
                    var isComment = patterns.Any(x => x.IsComment(text));
                    
                    var commandId = isComment ? VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK : VSConstants.VSStd2KCmdID.COMMENT_BLOCK;

                    ExecuteCommand(commandId);
                }
                else if (ExecuteCommand(VSConstants.VSStd2KCmdID.COMMENT_BLOCK) == false)
                {
                    ShowMessageBox(
                        "Toggle Comment is not executable.",
                        $"{textDocument.Language} files is not supported.",
                        OLEMSGICON.OLEMSGICON_INFO);
                }
            }
        }

        /// <summary>
        /// コードのコメントを表すパターンを作成します。
        /// </summary>
        private static ICodeCommentPattern[] CreateCommentPatterns(string language)
        {
            switch (language)
            {
                case "CSharp":
                case "C/C++":
                case "TypeScript":
                    {
                        return new ICodeCommentPattern[] { new LineCommentPattern("//"), new BlockCommentPattern("/*", "*/") };
                    }
                case "XML":
                case "XAML":
                    {
                        return new[] { new BlockCommentPattern("<!--", "-->") };
                    }
                case "HTMLX":
                    {
                        // MEMO : HTML に埋め込まれたCSS, JavaScriptをサポートする
                        return new ICodeCommentPattern[] {
                            new BlockCommentPattern("<!--", "-->"),
                            new BlockCommentPattern("@*", "*@"),
                            new BlockCommentPattern("/*", "*/"),
                            new LineCommentPattern("//")};
                    }
                case "HTML":
                    {
                        // MEMO : VS の UncommentSelection コマンドがブロックコメント <%/* */%> に対応していない
                        return new ICodeCommentPattern[] {
                            new BlockCommentPattern("<!--", "-->"),
                            new BlockCommentPattern("<%--", "--%>"),
                            new BlockCommentPattern("/*", "*/"),
                            new LineCommentPattern("//")};
                    }
                case "JavaScript":
                case "F#":
                    {
                        // MEMO : VS の UncommentSelection コマンドが JavaScript, F# のブロックコメントに対応していない
                        return new[] { new LineCommentPattern("//") };
                    }
                case "CSS":
                    {
                        return new[] { new BlockCommentPattern("/*", "*/") };
                    }
                case "PowerShell":
                    {
                        // MEMO : VS の UncommentSelection コマンドが PowerShell のブロックコメントに対応していない
                        return new[] { new LineCommentPattern("#") };
                    }
                case "SQL Server Tools":
                    {
                        return new[] { new LineCommentPattern("--") };
                    }
                case "Basic":
                    {
                        return new[] { new LineCommentPattern("'") };
                    }
                case "Python":
                    {
                        return new[] { new LineCommentPattern("#") };
                    }
                default:
                    {
                        return new ICodeCommentPattern[0];
                    }
            }
        }

        /// <summary>
        /// 指定のコマンドを実行します。
        /// コマンドが実行できなかった場合は<see langword="false"/>を返します。
        /// </summary>
        private bool ExecuteCommand(VSConstants.VSStd2KCmdID commandId)
        {
            var grooupId = VSConstants.VSStd2K;
            var result = _commandTarget.Exec(ref grooupId, (uint)commandId, 0, IntPtr.Zero, IntPtr.Zero);

            return result == VSConstants.S_OK;
        }

        /// <summary>
        /// 選択中の全ての行のテキストを取得します。
        /// </summary>
        private static string GetTextOfSelectedLines(TextSelection selection)
        {
            var startPoint = selection.TopPoint.CreateEditPoint();
            startPoint.StartOfLine();

            var endPoint = selection.BottomPoint.CreateEditPoint();
            endPoint.EndOfLine();
//             if (startPoint.Line == endPoint.Line)
//             {
//                 startPoint.StartOfLine();
//                 endPoint.EndOfLine();
//             }

            if (selection.Mode == vsSelectionMode.vsSelectionModeBox)
            {
                selection.Mode = vsSelectionMode.vsSelectionModeStream;
            }

            return startPoint.GetText(endPoint);
        }
    }
}
