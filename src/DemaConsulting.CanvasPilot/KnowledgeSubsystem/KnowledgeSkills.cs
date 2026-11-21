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

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     Describes the cards of a catalog as skills for an assistant: one skill for each card, with a name that is made
///     from the identifier of the card, the title and summary of the card as its description, and the text of the card with
///     its examples as its content.
/// </summary>
/// <remarks>
///     <para>
///         The skills are made one at a time as the caller reads them, because the text of all the cards is many times what
///         should be built up front. The name of a skill is <c>canvasnet-</c> and the identifier of the card with each
///         <c>/</c> written as <c>-</c>, for the cards of CanvasPilot as well, so <c>svg/framed-figure</c> gives
///         <c>canvasnet-svg-framed-figure</c>. Two cards could in theory give the same name, such as <c>a/b-c</c> and
///         <c>a-b/c</c>; the tests of the unit check that the cards of the application do not, so a new card that does
///         fails the build. The unit holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
public static class KnowledgeSkills
{
    /// <summary>
    ///     The start of the name of every skill.
    /// </summary>
    public const string NamePrefix = "canvasnet-";

    /// <summary>
    ///     Makes the name of the skill of a card.
    /// </summary>
    /// <param name="cardId">The identifier of the card.</param>
    /// <returns>The name: <see cref="NamePrefix" /> and the identifier with each <c>/</c> written as <c>-</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cardId" /> is <see langword="null" />.</exception>
    public static string NameFor(string cardId)
    {
        ArgumentNullException.ThrowIfNull(cardId);

        return NamePrefix + cardId.Replace('/', '-');
    }

    /// <summary>
    ///     Describes every card of a catalog as a skill.
    /// </summary>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <returns>
    ///     One skill for each card, in the order of the cards. The skills are made as the result is read, not before.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> is <see langword="null" />.</exception>
    public static IEnumerable<KnowledgeSkill> Create(IKnowledgeCatalog catalog)
    {
        // The argument is checked here so that a missing catalog is refused at the call and not when the result is read
        ArgumentNullException.ThrowIfNull(catalog);

        return CreateSkills(catalog);
    }

    /// <summary>
    ///     Makes the skills one at a time.
    /// </summary>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <returns>The skills.</returns>
    private static IEnumerable<KnowledgeSkill> CreateSkills(IKnowledgeCatalog catalog)
    {
        var prompts = new KnowledgePromptBuilder(catalog);
        foreach (var card in catalog.Cards)
        {
            yield return new KnowledgeSkill(
                NameFor(card.Id),
                $"{card.Title} - {card.Summary}",
                prompts.BuildCard(card, includeExamples: true));
        }
    }
}
