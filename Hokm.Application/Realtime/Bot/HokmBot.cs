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

            var bestSuitSelection = firstFiveCards
                .GroupBy(c => c.Suit)
                .Select(g => new
                {
                    Suit = g.Key,
                    Count = g.Count(),
                    MaxRank = g.Max(c => (int)c.Rank)
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.MaxRank)
                .First();

            return bestSuitSelection.Suit;
        }
        public static Card DecideCardToPlay(Game game, Guid playerId)
        {
            var currentRound = game.Rounds[game.CurrentRoundIndex!.Value];
            var currentTrick = currentRound.Tricks.Last(t => !t.IsComplete);
            var playerHand = currentRound.PlayerHands[playerId];
            var trumpSuit = currentRound.TrumpSuit!.Value;

            // ۱. لیست کارت‌های مجاز برای بازی در این نوبت
            var playableCards = playerHand.Where(card => game.IsCardPlayable(playerId, card)).ToList();

            // سناریو الف: ربات اول دست است (زمین خالی است)
            if (currentTrick.PlayedCards.Count == 0)
            {
                return DecideAsLead(playableCards, trumpSuit, playerId, game, currentRound);
            }

            // سناریو ب: وسط بازی است و کارت‌های دیگران روی زمین است
            return DecideAsFollower(playableCards, currentTrick, trumpSuit, playerId, game);
        }

        private static Card DecideAsLead(List<Card> playableCards, Suit trumpSuit, Guid playerId, Game game, Round round)
        {
            // قانون اول: استراتژی حکم‌کشی (اگر ربات حاکم است و دست‌های اول بازی است)
            // اگر بیش از ۳ حکم در دست دارد، یک حکم بزرگ (آس یا شاه یا بی بی) بکشد تا حکم‌های زمین جمع شوند
            var botTrumps = playableCards.Where(c => c.Suit == trumpSuit).OrderByDescending(c => c.Rank).ToList();
            bool isFirstFewTricks = round.Tricks.Count <= 3;
            if (isFirstFewTricks && botTrumps.Count >= 3)
            {
                var highTrump = botTrumps.FirstOrDefault(c => c.Rank >= Rank.Ten);
                if (highTrump != null) return highTrump;
            }

            // قانون دوم: جفتِ آس و شاه (Ace-King Combo)
            // اگر از یک خال هم آس و هم شاه را دارد، ابتدا آس را بازی می‌کند چون کاملاً امن است
            var suitsInHand = playableCards.Select(c => c.Suit).Distinct();
            foreach (var suit in suitsInHand)
            {
                if (suit == trumpSuit) continue;
                var hasAce = playableCards.Any(c => c.Suit == suit && c.Rank == Rank.Ace);
                var hasKing = playableCards.Any(c => c.Suit == suit && c.Rank == Rank.King);
                if (hasAce && hasKing)
                {
                    return playableCards.First(c => c.Suit == suit && c.Rank == Rank.Ace);
                }
            }

            // قانون سوم: آس غیر حکم (فقط در صورتی که حداقل یک پشتیبان پشتش باشد، یعنی تک آس نباشد)
            var safeAce = playableCards.FirstOrDefault(c =>
                c.Suit != trumpSuit &&
                c.Rank == Rank.Ace &&
                playableCards.Count(x => x.Suit == c.Suit) > 1);
            if (safeAce != null) return safeAce;

            // قانون چهارم: اگر کارت سر نداشت، یک کارت کوچک از خالِ شلوغ خود بازی کند تا دست‌های بعدی سر شود
            var bestLongSuitCard = playableCards
                .Where(c => c.Suit != trumpSuit)
                .GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count()) // خالی که بیشتر از همه دارد
                .Select(g => g.OrderBy(c => c.Rank).First()) // کوچکترین کارت آن خال
                .FirstOrDefault();

            if (bestLongSuitCard != null) return bestLongSuitCard;

            // در نهایت اگر مجبور شد، کوچکترین کارت ممکن را بازی کند
            return playableCards.OrderBy(c => c.Rank).First();
        }

        private static Card DecideAsFollower(List<Card> playableCards, Trick trick, Suit trumpSuit, Guid botPlayerId, Game game)
        {
            var ledSuit = trick.LedSuit!.Value;

            // پیدا کردن بهترین کارت روی زمین و برنده فعلی دست
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

            // پیدا کردن شناسه یارِ ربات
            var botTeam = game.Teams.First(t => t.PlayerIds.Contains(botPlayerId));
            var partnerId = botTeam.PlayerIds.First(id => id != botPlayerId);
            bool partnerIsWinning = currentWinnerId == partnerId;

            var cardsOfLedSuit = playableCards.Where(c => c.Suit == ledSuit).ToList();

            if (cardsOfLedSuit.Any()) // ربات خال زمینه را دارد
            {
                if (partnerIsWinning)
                {
                    // یار برنده است؛ ربات ضعیف‌ترین کارت این خال را رد می‌دهد (پرت می‌کند)
                    return cardsOfLedSuit.OrderBy(c => c.Rank).First();
                }
                else
                {
                    // حریف برنده است؛ بررسی می‌کنیم آیا کارت برنده داریم؟
                    var winningCards = cardsOfLedSuit.Where(c => c.Beats(bestCardOnTable!, trumpSuit, ledSuit)).ToList();
                    if (winningCards.Any())
                    {
                        // قانون برش اقتصادی: کوچکترین کارتی که حریف را می‌زند بازی کن (نه بزرگترین کارت دستت را)
                        return winningCards.OrderBy(c => c.Rank).First();
                    }
                    // اگر نمی‌توانیم حریف را بزنیم، کارت ضعیف پرت کنیم تا کارت خوبمان نسوزد
                    return cardsOfLedSuit.OrderBy(c => c.Rank).First();
                }
            }
            else // ربات خال زمینه را ندارد (باید رد کند یا ببُرد)
            {
                if (partnerIsWinning)
                {
                    // یار برنده است؛ پس با خیال راحت یک کارت ضعیف غیرحکم را رد (پرت) می‌کنیم
                    var lowCard = playableCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.Rank).FirstOrDefault();
                    return lowCard ?? playableCards.OrderBy(c => c.Rank).First();
                }
                else
                {
                    // حریف برنده است؛ اگر حکم داریم دست را ببُریم
                    var trumps = playableCards.Where(c => c.Suit == trumpSuit).ToList();
                    if (trumps.Any())
                    {
                        // قانون برش اقتصادی با حکم:
                        // با کوچکترین حکمی که از حکم روی زمین (در صورت وجود) بزرگتر است، کات کن
                        var winningTrumps = trumps.Where(c => c.Beats(bestCardOnTable!, trumpSuit, ledSuit)).ToList();
                        if (winningTrumps.Any())
                        {
                            return winningTrumps.OrderBy(c => c.Rank).First();
                        }

                        // اگر روی زمین حکمی نبوده، با کوچکترین حکم خود دست را ببُر
                        if (bestCardOnTable!.Suit != trumpSuit)
                        {
                            return trumps.OrderBy(c => c.Rank).First();
                        }
                    }

                    // اگر حکم نداریم یا نمی‌توانیم ببُریم، ضعیف‌ترین کارت غیر حکم خود را پرت کنیم
                    var throwawayCard = playableCards.Where(c => c.Suit != trumpSuit).OrderBy(c => c.Rank).FirstOrDefault();
                    return throwawayCard ?? playableCards.OrderBy(c => c.Rank).First();
                }
            }
        }
    }
}
