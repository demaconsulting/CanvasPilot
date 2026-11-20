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

namespace DemaConsulting.CanvasPilot.AnnotationSubsystem;

/// <summary>
///     What the tool bar of the annotation layer is bound to: the layer's view model, which holds the chosen tool, the
///     note, and what is said to the user. The tool bar is placed in the top bar of the preview pane, outside the scaled
///     picture, so that its buttons keep their size at every zoom; it is a view model of its own only so that the view
///     locator finds its view.
/// </summary>
public sealed class AnnotationToolbarViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationToolbarViewModel" /> class.
    /// </summary>
    /// <param name="annotation">The view model of the annotation layer that the tool bar controls.</param>
    /// <exception cref="ArgumentNullException">Thrown when the view model of the layer is <see langword="null" />.</exception>
    public AnnotationToolbarViewModel(AnnotationOverlayViewModel annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        Annotation = annotation;
    }

    /// <summary>
    ///     Gets the view model of the annotation layer that the tool bar controls.
    /// </summary>
    public AnnotationOverlayViewModel Annotation { get; }
}
