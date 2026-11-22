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

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     What the Ollama server said when it was asked for its models.
/// </summary>
/// <param name="Reachable">
///     <see langword="true" /> when the server answered with a list of models; <see langword="false" /> when it did not
///     answer in time, refused the connection, or answered with something that is not a list of models.
/// </param>
/// <param name="Models">The names of the models that the server has, such as <c>gemma3:latest</c>; empty when unreachable.</param>
public sealed record OllamaModelList(bool Reachable, IReadOnlyList<string> Models)
{
    /// <summary>
    ///     Makes the answer of a server that could not be reached.
    /// </summary>
    public static OllamaModelList Unreachable { get; } = new(false, []);
}

/// <summary>
///     Asks an Ollama server which models it has and how large a context window a model supports. The interface is the
///     seam that lets the readiness check be tested without a server.
/// </summary>
public interface IOllamaProbe
{
    /// <summary>
    ///     Asks the server for the models that it has.
    /// </summary>
    /// <param name="endpoint">The address of the server.</param>
    /// <param name="timeout">The longest time to wait; a server that does not answer by then is unreachable.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The answer; a server that cannot be reached is a result, not an exception.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    Task<OllamaModelList> ListModelsAsync(Uri endpoint, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asks the server for the largest context window that a model supports.
    /// </summary>
    /// <param name="endpoint">The address of the server.</param>
    /// <param name="model">The name of the model, as the server lists it.</param>
    /// <param name="timeout">The longest time to wait.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>
    ///     The number of tokens, or <see langword="null" /> when the server did not say, did not answer in time, or
    ///     answered in a form that this check does not know; the answer is optional, so there is no error.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    Task<int?> GetContextLengthAsync(Uri endpoint, string model, TimeSpan timeout, CancellationToken cancellationToken = default);
}
