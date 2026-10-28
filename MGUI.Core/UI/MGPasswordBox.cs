using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI
{
    /// <summary>A synchronous consumer of a temporary password view. The view is valid only during the callback.</summary>
    public delegate void PasswordReadCallback(ReadOnlySpan<char> Password);

    public class MGPasswordBox : MGTextBox
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private char _PasswordCharacter;
		/// <summary>The character to display for each character in the text. Default value: <b>*</b><para/>
		/// Recommended to use a character that belongs to a common character set, to avoid issues where the <see cref="SpriteFont"/> cannot render the character.</summary>
		public char PasswordCharacter
		{
			get => _PasswordCharacter;
			set
			{
				if (_PasswordCharacter != value)
				{
					_PasswordCharacter = value;
                    if (!string.IsNullOrEmpty(Text))
                    {
                        _ = base.SetText(ReplaceNormalCharactersWith(Text, PasswordCharacter), true);
                    }
					NPC(nameof(PasswordCharacter));
				}
			}
		}

		protected override string GetTextBackingField() => Password ?? string.Empty;
		protected override void SetTextBackingField(string Value)
		{
            _Password?.Dispose();
			_Password = Value.AsSecureString();
			NPC(nameof(Password));
        }

        private int SecretGeneration;

        protected override bool SetText(string Value, bool ExecuteEvenIfSameValue)
        {
            int EditGeneration = ++SecretGeneration;
            SecureString Temp = Value?.AsSecureString();
            try
            {
                string MaskedValue = ReplaceNormalCharactersWith(Value, PasswordCharacter);
                if (!base.SetText(MaskedValue, true) || EditGeneration != SecretGeneration)
                {
                    return false;
                }
                _Password?.Dispose();
                _Password = Temp;
                Temp = null;
                NPC(nameof(Password));
                PasswordChanged?.Invoke(this, EventArgs.Empty);
                return EditGeneration == SecretGeneration;
            }
            finally
            {
                Temp?.Dispose();
            }
        }

        private static string ReplaceNormalCharactersWith(string s, char c)
		{
			List<char> SpecialCharacters = new() { '\n', '\r' };
			if (s == null)
				return null;
			else
			{
				StringBuilder SB = new(s.Length);
				foreach (char Character in s)
				{
					if (SpecialCharacters.Contains(Character))
						SB.Append(Character);
					else
						SB.Append(c);
				}
				return SB.ToString();
			}
		}

		private SecureString _Password;
		/// <summary>Decodes the <see cref="SecureString"/> holding this <see cref="MGPasswordBox"/>'s Password into a plain-text string and returns the result.</summary>
        public string Password
        {
            get
            {
                string Result = string.Empty;
                ReadPassword(Value => Result = new string(Value));
                return Result;
            }
        }

        /// <summary>Invokes a synchronous callback with the exact password. Owned temporary memory is zeroed even if the callback throws.
        /// Existing editing operations and the legacy Password property still create managed strings that cannot be erased.</summary>
        public void ReadPassword(PasswordReadCallback Reader)
        {
            ArgumentNullException.ThrowIfNull(Reader);
            char[] Buffer = new char[_Password?.Length ?? 0];
            ReadPasswordCore(_Password, Buffer, Reader);
        }

        private static void ReadPasswordCore(SecureString Secret, char[] Buffer, PasswordReadCallback Reader)
        {
            IntPtr NativeBuffer = IntPtr.Zero;
            try
            {
                if (Secret != null && Buffer.Length > 0)
                {
                    NativeBuffer = Marshal.SecureStringToGlobalAllocUnicode(Secret);
                    Marshal.Copy(NativeBuffer, Buffer, 0, Buffer.Length);
                }
                Reader(Buffer);
            }
            finally
            {
                try
                {
                    ZeroPasswordBuffers(Buffer, NativeBuffer);
                }
                finally
                {
                    if (NativeBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(NativeBuffer);
                    }
                }
            }
        }

        private static void ZeroPasswordBuffers(char[] Buffer, IntPtr NativeBuffer)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(Buffer.AsSpan()));
            if (NativeBuffer != IntPtr.Zero)
            {
                Marshal.Copy(Buffer, 0, NativeBuffer, Buffer.Length);
            }
        }

        /// <summary>Clears the secret, masked display, history, selection, caret and pending editing state.
        /// Cleanup cannot be cancelled by TextChanging and may be called repeatedly.</summary>
        public void ClearPassword()
        {
            SecretGeneration++;
            bool Changed = (_Password?.Length ?? 0) > 0 || Text.Length > 0;
            _Password?.Dispose();
            _Password = new SecureString();
            ClearTextAndEditingState();
            if (Changed)
            {
                NPC(nameof(Password));
                PasswordChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected sealed override bool SupportsUndoRedo => false;

		public event EventHandler<EventArgs> PasswordChanged;

		/// <summary>This feature is intentionally disabled for <see cref="MGPasswordBox"/></summary>
        public override bool AcceptsReturn { get => false; set { } }
        /// <summary>This feature is intentionally disabled for <see cref="MGPasswordBox"/></summary>
        public override bool AcceptsTab { get => false; set { } }

        public MGPasswordBox(MGWindow Window, int? CharacterLimit = 250)
            : base(Window, MGElementType.PasswordBox, CharacterLimit, false, false)
        {
			using (BeginInitializing())
			{
                UndoRedoHistorySize = 0;
				PasswordCharacter = '*';
				AcceptsReturn = false;
				AcceptsTab = false;
			}
        }
    }
}
