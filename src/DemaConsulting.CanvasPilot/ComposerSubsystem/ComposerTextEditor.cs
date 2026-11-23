// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using AvaloniaEdit;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The editing surface of the message, made known to assistive technology: screen readers and automation tools see
///     it as a text field that has a name and a value, and can read and replace the message through the value.
/// </summary>
/// <remarks>
///     <para>
///         The library's editing control has no automation peer of its own, so the platform reports it as an unnamed
///         scroll area without text. This control only adds a peer that is an edit field with a value pattern; it does
///         not change how the surface draws or edits. The text that the peer reports is not the raw text of the surface,
///         which holds one placeholder character for each chip, but the text that the owner gives through
///         <see cref="ValueProvider" />, in which each chip is named in brackets. A new value is given back to the owner
///         through <see cref="ValueSetter" />, so the rules of editing stay with the message and not with the peer.
///     </para>
/// </remarks>
public sealed class ComposerTextEditor : TextEditor
{
    /// <summary>
    ///     Gets or sets what tells the text of the message as a reader should hear it, or <see langword="null" /> for the
    ///     text of the surface.
    /// </summary>
    public Func<string>? ValueProvider { get; set; }

    /// <summary>
    ///     Gets or sets what replaces the message with a text that automation sets, or <see langword="null" /> when it
    ///     cannot be replaced that way.
    /// </summary>
    public Action<string>? ValueSetter { get; set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ComposerTextEditorAutomationPeer(this);

    /// <summary>
    ///     The peer of the editing surface: an edit field that holds the text of the message.
    /// </summary>
    private sealed class ComposerTextEditorAutomationPeer : ControlAutomationPeer, IValueProvider
    {
        /// <summary>
        ///     The text that was last reported, to report a change of it only once.
        /// </summary>
        private string? _reported;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ComposerTextEditorAutomationPeer" /> class.
        /// </summary>
        /// <param name="owner">The editing surface.</param>
        public ComposerTextEditorAutomationPeer(ComposerTextEditor owner)
            : base(owner)
        {
            _reported = ReadValue();
            owner.TextChanged += OnTextChanged;
        }

        /// <summary>
        ///     Gets the editing surface.
        /// </summary>
        private new ComposerTextEditor Owner => (ComposerTextEditor)base.Owner;

        /// <inheritdoc />
        public bool IsReadOnly => Owner.IsReadOnly || Owner.ValueSetter is null;

        /// <inheritdoc />
        public string? Value => ReadValue();

        /// <inheritdoc />
        public void SetValue(string? value)
        {
            if (!IsReadOnly)
            {
                Owner.ValueSetter!(value ?? string.Empty);
            }
        }

        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

        /// <inheritdoc />
        protected override bool IsKeyboardFocusableCore() => true;

        /// <inheritdoc />
        protected override bool HasKeyboardFocusCore() => Owner.TextArea.IsFocused;

        /// <inheritdoc />
        protected override void SetFocusCore() => Owner.TextArea.Focus();

        /// <summary>
        ///     Reads the text that is reported for the message.
        /// </summary>
        /// <returns>The text.</returns>
        private string ReadValue() => Owner.ValueProvider?.Invoke() ?? Owner.Text;

        /// <summary>
        ///     Tells assistive technology that the message changed.
        /// </summary>
        /// <param name="sender">The editing surface.</param>
        /// <param name="e">The event data.</param>
        private void OnTextChanged(object? sender, EventArgs e)
        {
            var current = ReadValue();
            if (string.Equals(current, _reported, StringComparison.Ordinal))
            {
                return;
            }

            var previous = _reported;
            _reported = current;
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, previous, current);
        }
    }
}
