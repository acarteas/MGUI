using System.Reflection;
using System.Security;
using System.Runtime.InteropServices;
using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.RenderCacheTestHost;

internal sealed class PasswordInputTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;

    private PasswordInputTests()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using PasswordInputTests game = new();
        game.Run();
        Check(game.Completed, "Password input checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        return 0;
    }

    protected override void LoadContent()
    {
        using LiveSiblingFixture fixture = new(this, 1, 1, cached: false);
        SnapshotProbe password = new(fixture.Window, 1024) { PreferredWidth = 300, PreferredHeight = 60 };
        void Step(params Keys[] keys)
        {
            fixture.Keyboard = new(keys);
            fixture.Tick();
        }
        void Press(params Keys[] keys)
        {
            Step(keys);
            Step();
        }
        void Focus(MGTextBox box)
        {
            fixture.Window.SetContent(box);
            for (int frame = 0; frame < 3; frame++)
            {
                Step();
            }
            box.RequestFocus();
            Step();
            Check(fixture.Desktop.FocusedKeyboardHandler == box, "Keyboard focus was not assigned.");
            Check(box.Caret.MoveToOriginalCharacterIndexOrEnd(box.Text.Length, true), "Caret could not be positioned.");
        }
        void SecretIs(string expected)
        {
            password.ReadPassword(value => Check(value.SequenceEqual(expected), "Scoped secret differs from input."));
        }
        void NoHistory(MGTextBox box)
        {
            foreach (string name in new[] { "UndoStack", "RedoStack" })
            {
                object stack = typeof(MGTextBox).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(box)!;
                Check((int)stack.GetType().GetProperty("Count")!.GetValue(stack)! == 0, "Disabled history retained a state.");
            }
            Check(!box.TryUndo() && !box.TryRedo(), "Disabled history restored a state.");
        }
        StringClipboard Clipboard(MGTextBox box) => (StringClipboard)typeof(MGTextBox)
            .GetProperty("Clipboard", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(box)!;

        int changing = 0, changed = 0, passwordChanged = 0;
        password.TextChanging += (_, args) =>
        {
            changing++;
            Check(args.Data.All(value => value is '*' or '#'), "TextChanging exposed plaintext.");
        };
        password.TextChanged += (_, args) =>
        {
            changed++;
            Check(args.PreviousValue.All(value => value is '*' or '#') && args.NewValue.All(value => value is '*' or '#'),
                "TextChanged exposed plaintext.");
        };
        password.PasswordChanged += (_, _) => passwordChanged++;
        Check(password.Password == string.Empty, "An empty password no longer returns an empty string.");
        Focus(password);
        Press(Keys.A);
        Press(Keys.B);
        SecretIs("ab");
        Check(password.Text == "**" && password.FormattedText == "**", "Typing display was not masked.");
        Check(!password.ToString()!.Contains("ab", StringComparison.Ordinal), "ToString exposed plaintext.");
        NoHistory(password);
        Results["typing-and-masked-events"] = changing > 0 && changed > 0 && passwordChanged > 0;

        MGTextBox inherited = password;
        inherited.UndoRedoHistorySize = 20;
        int previousReads = password.BackingReads;
        object? disabledSnapshot = typeof(MGTextBox).GetMethod("CreateRestorableState", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(password, null);
        Check(disabledSnapshot == null && password.BackingReads == previousReads, "Disabled snapshot capture read plaintext backing.");
        Check(inherited.UndoRedoHistorySize == 0, "Password history was enabled through the base type.");
        try
        {
            inherited.UndoRedoHistorySize = -1;
            throw new InvalidOperationException("Negative history size was accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
            Check(inherited.UndoRedoHistorySize == 0, "Rejected size mutated password policy.");
        }
        password.SelectAll();
        Press(Keys.C);
        SecretIs("c");
        NoHistory(password);
        Press(Keys.LeftControl, Keys.Z);
        Press(Keys.LeftControl, Keys.Y);
        SecretIs("c");
        Results["immutable-no-history"] = true;

        Clipboard(password).Text = "paste-\u00e9\U0001f680";
        password.SelectAll();
        Press(Keys.LeftControl, Keys.V);
        SecretIs("paste-\u00e9\U0001f680");
        password.SelectAll();
        Press(Keys.LeftControl, Keys.C);
        Check(Clipboard(password).Text.All(value => value is '*' or '#'), "Copy exposed plaintext.");
        Press(Keys.LeftControl, Keys.X);
        SecretIs("");
        Check(Clipboard(password).Text.All(value => value is '*' or '#'), "Cut exposed plaintext.");
        NoHistory(password);
        Clipboard(password).Text = "keyboard";
        Check(password.Caret.MoveToOriginalCharacterIndexOrEnd(0, true), "Caret could not be reset.");
        Press(Keys.LeftControl, Keys.V);
        Press(Keys.Back);
        SecretIs("keyboar");
        password.SelectAll();
        Press(Keys.Delete);
        SecretIs("");
        NoHistory(password);
        Clipboard(password).Text = "replace";
        Check(password.Caret.MoveToOriginalCharacterIndexOrEnd(0, true), "Caret could not be reset for paste.");
        Press(Keys.LeftControl, Keys.V);
        password.SelectAll();
        Press(Keys.Back);
        SecretIs("");
        NoHistory(password);
        Clipboard(password).Text = "abc";
        Check(password.Caret.MoveToOriginalCharacterIndexOrEnd(0, true), "Caret could not be reset for overwrite.");
        Press(Keys.LeftControl, Keys.V);
        Press(Keys.Home);
        Press(Keys.Insert);
        Press(Keys.X);
        SecretIs("xbc");
        Press(Keys.Delete);
        SecretIs("xc");
        Press(Keys.LeftControl, Keys.D);
        NoHistory(password);
        password.ClearPassword();
        Results["paste-copy-cut-backspace-delete"] = true;

        const string unicode = "A\u00e9e\u0301\U0001f680\u0000Z";
        password.SetText(unicode);
        SecretIs(unicode);
        Check(password.Password == unicode, "Legacy password conversion truncated Unicode.");
        password.PasswordCharacter = '#';
        SecretIs(unicode);
        Check(password.Text == new string('#', unicode.Length), "Remasking changed the display length.");
        password.PasswordCharacter = '*';
        SecretIs(unicode);
        Results["exact-unicode-and-remasking"] = true;

        try
        {
            password.ReadPassword(value =>
            {
                Check(value.SequenceEqual(unicode), "Throwing callback did not receive exact Unicode.");
                throw new CallbackFailure();
            });
            throw new InvalidOperationException("Callback exception was swallowed.");
        }
        catch (CallbackFailure)
        {
            SecretIs(unicode);
        }
        MethodInfo readCore = typeof(MGPasswordBox).GetMethod("ReadPasswordCore", BindingFlags.NonPublic | BindingFlags.Static)!;
        using (SecureString secret = new())
        {
            foreach (char value in unicode)
            {
                secret.AppendChar(value);
            }
            foreach (bool shouldThrow in new[] { false, true })
            {
                char[] ownedBuffer = new char[unicode.Length];
                PasswordReadCallback callback = value =>
                {
                    Check(value.SequenceEqual(unicode) && ownedBuffer.AsSpan().SequenceEqual(unicode), "Core did not use its owned buffer.");
                    if (shouldThrow)
                    {
                        throw new CallbackFailure();
                    }
                };
                try
                {
                    readCore.Invoke(null, new object[] { secret, ownedBuffer, callback });
                    Check(!shouldThrow, "Throwing core callback did not throw.");
                }
                catch (TargetInvocationException exception) when (exception.InnerException is CallbackFailure)
                {
                    Check(shouldThrow, "Unexpected callback failure.");
                }
                Check(ownedBuffer.All(value => value == '\0'), "Scoped temporary was not zeroed.");
            }
            IntPtr nativeBuffer = Marshal.SecureStringToGlobalAllocUnicode(secret);
            try
            {
                char[] nativeCopy = new char[unicode.Length];
                Marshal.Copy(nativeBuffer, nativeCopy, 0, nativeCopy.Length);
                Check(nativeCopy.AsSpan().SequenceEqual(unicode), "Native wipe fixture lacked the exact secret.");
                MethodInfo wipe = typeof(MGPasswordBox).GetMethod("ZeroPasswordBuffers", BindingFlags.NonPublic | BindingFlags.Static)!;
                wipe.Invoke(null, new object[] { nativeCopy, nativeBuffer });
                Check(nativeCopy.All(value => value == '\0'), "Managed wipe helper retained characters.");
                Marshal.Copy(nativeBuffer, nativeCopy, 0, nativeCopy.Length);
                Check(nativeCopy.All(value => value == '\0'), "Native wipe missed characters after an embedded NUL.");
            }
            finally
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
        Results["scoped-temporary-zero-success-and-throw"] = true;

        password.SelectAll();
        password.InitialKeyRepeatDelay = TimeSpan.Zero;
        password.KeyRepeatInterval = TimeSpan.Zero;
        Step(Keys.Q);
        password.TextChanging += (_, args) => args.Cancel = true;
        password.ClearPassword();
        password.ClearPassword();
        Check(password.Text == "" && password.FormattedText == "" && password.CurrentSelection == null && !password.Caret.HasPosition,
            "Cleanup retained display or editing state.");
        Step(Keys.Q);
        Step();
        SecretIs("");
        Check(typeof(MGTextBox).GetProperty("LastKeyPress", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(password) == null,
            "Cleanup retained a key-repeat input.");
        NoHistory(password);
        Results["noncancellable-idempotent-clear-no-repeat"] = true;

        MGPasswordBox changingCleanup = new(fixture.Window);
        changingCleanup.TextChanging += (_, _) => changingCleanup.ClearPassword();
        Check(!changingCleanup.SetText("cancelled-in-flight"), "TextChanging cleanup was overwritten.");
        changingCleanup.ReadPassword(value => Check(value.IsEmpty, "TextChanging cleanup resurrected the secret."));
        Check(changingCleanup.Text == "" && changingCleanup.FormattedText == "", "TextChanging cleanup resurrected masks.");
        MGPasswordBox reentrant = new(fixture.Window);
        bool clearOnChange = true;
        reentrant.TextChanged += (_, _) =>
        {
            if (clearOnChange)
            {
                clearOnChange = false;
                reentrant.ClearPassword();
            }
        };
        Check(!reentrant.SetText("in-flight"), "A cleared in-flight edit was committed.");
        reentrant.ReadPassword(value => Check(value.IsEmpty, "TextChanged cleanup resurrected the secret."));
        reentrant.PasswordChanged += (_, _) => reentrant.ClearPassword();
        reentrant.SetText("second-edit");
        reentrant.ReadPassword(value => Check(value.IsEmpty, "PasswordChanged cleanup resurrected the secret."));
        Check(reentrant.Text == "" && reentrant.FormattedText == "", "Reentrant cleanup retained masks.");
        MGPasswordBox remasking = new(fixture.Window);
        remasking.SetText("remask-secret");
        remasking.TextChanged += (_, _) => remasking.ClearPassword();
        remasking.PasswordCharacter = '#';
        remasking.ReadPassword(value => Check(value.IsEmpty, "Remasking cleanup resurrected the secret."));
        Focus(reentrant);
        Press(Keys.T);
        reentrant.ReadPassword(value => Check(value.IsEmpty, "Keyboard callback cleanup resurrected the secret."));
        Check(!reentrant.Caret.HasPosition && reentrant.CurrentSelection == null, "In-flight edit restored cleared caret/selection.");
        Results["reentrant-event-clear"] = true;

        MGTextBox normal = new(fixture.Window) { PreferredWidth = 300, PreferredHeight = 60 };
        Check(normal.UndoRedoHistorySize == MGTextBox.DefaultUndoRedoHistorySize, "Ordinary default history changed.");
        normal.SetText("before");
        Focus(normal);
        normal.SelectAll();
        Press(Keys.N);
        Check(normal.Text == "n" && normal.TryUndo() && normal.Text == "before" && normal.TryRedo() && normal.Text == "n",
            "Ordinary typing undo/redo regressed.");
        try
        {
            normal.UndoRedoHistorySize = -1;
            throw new InvalidOperationException("Negative ordinary history size was accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
            Check(normal.UndoRedoHistorySize == 20, "Rejected size mutated ordinary history.");
        }
        normal.ClearUndoRedoHistory();
        NoHistory(normal);
        Results["ordinary-undo-redo-and-explicit-clear"] = true;
        normal.UndoRedoHistorySize = 0;
        normal.SelectAll();
        Press(Keys.D);
        NoHistory(normal);
        normal.UndoRedoHistorySize = 1;
        normal.SelectAll();
        Press(Keys.E);
        normal.SelectAll();
        Press(Keys.F);
        Check(normal.TryUndo() && normal.Text == "e" && !normal.TryUndo(), "Reduced history limit was not respected.");
        Results["ordinary-zero-disable-and-size-limit"] = true;

        MGWindow xamlWindow = XAMLParser.LoadRootWindow(fixture.Desktop, """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Width="300" Height="100">
              <PasswordBox Name="Secret" UndoRedoHistorySize="99" CharacterLimit="1024" />
            </Window>
            """);
        fixture.Desktop.Windows.Add(xamlWindow);
        Check(xamlWindow.TryGetElementByName("Secret", out MGElement element) && element is MGPasswordBox,
            "XAML PasswordBox did not materialize.");
        MGPasswordBox xamlPassword = (MGPasswordBox)element;
        Check(xamlPassword.UndoRedoHistorySize == 0 && xamlPassword.CharacterLimit == 1024, "XAML enabled password history.");
        xamlPassword.SetText(unicode);
        for (int frame = 0; frame < 3; frame++)
        {
            Step();
        }
        xamlPassword.RequestFocus();
        Step();
        xamlPassword.SelectAll();
        Press(Keys.X);
        xamlPassword.ReadPassword(value => Check(value.SequenceEqual("x"), "XAML password did not receive actual input."));
        NoHistory(xamlPassword);
        xamlPassword.ClearPassword();
        Results["xaml-password-policy"] = true;
        Completed = true;
        Exit();
    }

    private sealed class SnapshotProbe : MGPasswordBox
    {
        internal int BackingReads { get; private set; }

        internal SnapshotProbe(MGWindow window, int? characterLimit) : base(window, characterLimit) { }

        protected override string GetTextBackingField()
        {
            BackingReads++;
            return base.GetTextBackingField();
        }
    }

    private sealed class CallbackFailure : Exception { }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
