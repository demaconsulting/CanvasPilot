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

using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace DemaConsulting.CanvasPilot.AppShellSubsystem;

/// <summary>
///     Convention-based view locator. Maps a view model whose type name ends in <c>ViewModel</c> to a
///     same-namespace, same-assembly control type with the same name but ending in <c>View</c> instead,
///     avoiding a source-generator dependency for this one-to-one mapping.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var type = ResolveViewType(data.GetType());
        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + data.GetType().FullName };
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        if (data is null)
        {
            return false;
        }

        return ResolveViewType(data.GetType()) is not null;
    }

    /// <summary>
    ///     Resolves the view control type that corresponds to a view model type.
    /// </summary>
    /// <param name="viewModelType">The view model type.</param>
    /// <returns>The matching view type, or <see langword="null"/> when none exists.</returns>
    private static Type? ResolveViewType(Type viewModelType)
    {
        var fullName = viewModelType.FullName;
        if (fullName is null || !fullName.EndsWith("ViewModel", StringComparison.Ordinal))
        {
            return null;
        }

        var viewName = fullName[..^"ViewModel".Length] + "View";
        var assemblyQualifiedName = $"{viewName}, {viewModelType.Assembly.FullName}";
        var type = Type.GetType(assemblyQualifiedName);

        return type is not null && typeof(Control).IsAssignableFrom(type) ? type : null;
    }
}
