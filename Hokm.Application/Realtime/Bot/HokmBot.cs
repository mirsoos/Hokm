using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Domain.ValueObjects;

namespace Hokm.Application.Realtime.Bot
{
    public static class HokmBot
    {
        public static Suit DecideTrump(List<Card> firstFiveCards)
        {
            if (firstFiveCards == null || firstFiveCards.Count != 5)
                throw new ArgumentException("Bot needs exactly 5 cards to choose trump.");

            // سیستم امتیازدهی حرفه‌ای به دست برای انتخاب هوشمندانه‌ترین حکم
            // آس = 4، شاه = 3، بی بی = 2، سرباز = 1 + وزن تعداد کارت‌های آن خال
            var bestSuitSelection = firstFiveCards
                .GroupBy(c => c.Suit)
                .Select(g => new
                {
                    Suit = g.Key,
                    Count = g.Count(),
                    PowerScore = (g.Count() * 3) + g.Sum(c => c.Rank switch
                    {
                        Rank.Ace => 4,
                        Rank.King => 3,
                        Rank.Queen => 2,
                        Rank.Jack => 1,
                        _ => 0
                    })
                })
                .OrderByDescending(x => x.PowerScore)
                .ThenByDescending(x => x.Count)
                .First();

            return bestSuitSelection.Suit;
        }

        public static Card DecideCardToPlay(Game game, Guid playerId)
        {
            var currentRound = game.Rounds[game.CurrentRoundIndex!.Value];
            var currentTrick = currentRound.Tricks.Last(t => !t.IsComplete);
            var playerHand = currentRound.PlayerHands[playerId];
            var trumpSuit = currentRound.TrumpSuit!.Value;

            var playableCards = playerHand.Where(card => game.IsCardPlayable(playerId, card)).ToList();

            if (playableCards.Count == 1)
                return playableCards[0];

            // استخراج تمام کارت‌های بازی‌شده در دست‌های قبلی برای آنالیز حرفه‌ای
            var playedCardsHistory = currentRound.Tricks
                .Where(t => t.IsComplete)
                .SelectMany(t => t.PlayedCards.Values)
                .Concat(currentTrick.PlayedCards.Values)
                .ToList();

            if (currentTrick.PlayedCards.Count == 0)
            {
                return DecideAsLead(playableCards, trumpSuit, playerId, game, currentRound, playedCardsHistory);
            }

            return DecideAsFollower(playableCards, currentTrick, trumpSuit, playerId, game, currentRound, playedCardsHistory);
        }

        private static Card DecideAsLead(
            List<Card> playableCards,
            Suit trumpSuit,
            Guid playerId,
            Game game,
            Round round,
            List<Card> playedHistory)
        {
            var botTrumps = playableCards.Where(c => c.Suit == trumpSuit).OrderByDescending(c => c.Rank).ToList();

            // ۱. حکم‌کشی حرفه‌ای: اگر حاکم هستیم یا دست بالا داریم و حکم دستمان زیاد است (بیش از ۲ تا)
            if (botTrumps.Count >= 3)
            {
                // اگر آس حکم داریم سریع بکشیم تا حکم‌های زمین خالی شوند
                var aceTrump = botTrumps.FirstOrDefault(c => c.Rank == Rank.Ace);
                if (aceTrump != null) return aceTrump;

                // اگر شاه حکم داریم و آس قبلاً رفته، شاه بالاترین است
                var highestTrump = botTrumps.First();
                if (IsHighestCardRemaining(highestTrump, trumpSuit, playedHistory))
                    return highestTrump;
            }

            // ۲. بازی کردن کارت‌های سر (Master Cards): کارت‌هایی که در حال حاضر بالاترین کارت آن خال در کل بازی هستند
            var nonTrumpCards = playableCards.Where(c => c.Suit != trumpSuit).ToList();
            foreach (var card in nonTrumpCards.OrderByDescending(c => c.Rank))
            {
                if (IsHighestCardRemaining(card, card.Suit, playedHistory))
                {
                    return card; // مثلاً تک آس، یا شاهی که آس آن قبلاً سوخته
                }
            }

            // ۳. خالی کردن تک‌خال‌ها (Short Suits): اگر از یک خال فقط یک کارت غیر سر داریم، بازی کنیم تا دست بعد ببُریم
            var singleCards = nonTrumpCards
                .GroupBy(c => c.Suit)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .OrderBy(c => c.Rank)
                .FirstOrDefault();

            if (singleCards != null && botTrumps.Any())
            {
                return singleCards;
            }

            // ۴. بازی کردن از خالِ بلند (Long Suit): خالی که بیشترین تعداد را داریم از پایین بازی می‌کنیم
            var bestLongSuitCard = nonTrumpCards
                .GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count())
                .Select(g => g.OrderBy(c => c.Rank).First())
                .FirstOrDefault();

            if (bestLongSuitCard != null) return bestLongSuitCard;

            // در نهایت پایین‌ترین کارت ممکن
            return playableCards.OrderBy(c => c.Rank).First();
        }

        private static Card DecideAsFollower(
            List<Card> playableCards,
            Trick trick,
            Suit trumpSuit,
            Guid botPlayerId,
            Game game,
            Round round,
            List<Card> playedHistory)
        {
            var ledSuit = trick.LedSuit!.Value;

            Card? bestCardOnTable = null;
            Guid? currentWinnerId = null;
            foreach (var entry in trick.PlayedCards)
            {
                if (bestCardOnTable == null || entry.Value.Beats(bestCardOnTable, trumpSuit, ledSuit))
                {
                    bestCardOnTable = entry.Value;
                    currentWinnerId = entry.Key;
                }
            }

            var botTeam = game.Teams.First(t => t.PlayerIds.Contains(botPlayerId));
            var partnerId = botTeam.PlayerIds.First(id => id != botPlayerId);
            bool partnerIsWinning = currentWinnerId == partnerId;
            bool isLastPlayer = trick.PlayedCards.Count == 3;

            var cardsOfLedSuit = playableCards.Where(c => c.Suit == ledSuit).ToList();

            // حالت ۱: ربات خال زمینه را در دست دارد
            if (cardsOfLedSuit.Any())
            {
                if (partnerIsWinning)
                {
                    // اگر یار با کارت سر (مثلاً آس) برنده است یا ما نفر آخر هستیم، کارت ریز رد بده
                    if (isLastPlayer || IsMasterCard(bestCardOnTable!, ledSuit, trumpSuit, playedHistory))
                    {
                        return cardsOfLedSuit.OrderBy(c => c.Rank).First();
                    }
                }

                // تلاش برای بردن دست با حداقل کارت ممکن
                var winningCards = cardsOfLedSuit.Where(c => c.Beats(bestCardOnTable!, trumpSuit, ledSuit)).ToList();
                if (winningCards.Any())
                {
                    // اگر نفر آخر هستیم، با کوچکترین کارت برنده می‌زنیم
                    if (isLastPlayer)
                    {
                        return winningCards.OrderBy(c => c.Rank).First();
                    }

                    // اگر نفر وسط هستیم، محکم می‌زنیم تا نفر بعد نتواند رویش بیاید (مثلاً با آس یا شاه)
                    return winningCards.OrderByDescending(c => c.Rank).First();
                }

                // اگر نمی‌توانیم ببریم، کم‌ارزش‌ترین کارت را می‌دهیم
                return cardsOfLedSuit.OrderBy(c => c.Rank).First();
            }

            // حالت ۲: ربات خال زمینه را ندارد (فرصت بریدن یا رد دادن)
            else
            {
                if (partnerIsWinning)
                {
                    // یار دست را برده؛ پس اصلاً حکم خرج نکن و کارت هرزه غیرحکم رد بده
                    var discard = playableCards
                        .Where(c => c.Suit != trumpSuit)
                        .OrderBy(c => c.Rank)
                        .FirstOrDefault();

                    return discard ?? playableCards.OrderBy(c => c.Rank).First();
                }
                else
                {
                    // حریف در حال بردن است؛ آیا می‌توانیم با حکم ببُریم؟
                    var trumps = playableCards.Where(c => c.Suit == trumpSuit).ToList();
                    if (trumps.Any())
                    {
                        var winningTrumps = trumps.Where(c => c.Beats(bestCardOnTable!, trumpSuit, ledSuit)).ToList();
                        if (winningTrumps.Any())
                        {
                            // با کوچکترین حکمی که حریف را می‌زند کات کن (اقتصادی‌ترین برش)
                            return winningTrumps.OrderBy(c => c.Rank).First();
                        }
                    }

                    // حکم نداریم یا نمی‌توانیم ببُریم؛ بی‌ارزش‌ترین کارت را رد بده
                    var discard = playableCards
                        .Where(c => c.Suit != trumpSuit)
                        .OrderBy(c => c.Rank)
                        .FirstOrDefault();

                    return discard ?? playableCards.OrderBy(c => c.Rank).First();
                }
            }
        }

        // متد کمکی: آیا این کارت در حال حاضر بالاترین کارت نسوخته در این خال است؟
        private static bool IsHighestCardRemaining(Card card, Suit suit, List<Card> playedHistory)
        {
            var higherRanks = Enum.GetValues<Rank>().Where(r => r > card.Rank);
            foreach (var rank in higherRanks)
            {
                // اگر کارتی با رنک بالاتر هنوز در بازی نسوخته باشد، پس این کارت سر نیست
                if (!playedHistory.Any(c => c.Suit == suit && c.Rank == rank))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsMasterCard(Card card, Suit ledSuit, Suit trumpSuit, List<Card> playedHistory)
        {
            if (card.Suit == trumpSuit)
                return IsHighestCardRemaining(card, trumpSuit, playedHistory);

            return card.Suit == ledSuit && IsHighestCardRemaining(card, ledSuit, playedHistory);
        }
    }
}