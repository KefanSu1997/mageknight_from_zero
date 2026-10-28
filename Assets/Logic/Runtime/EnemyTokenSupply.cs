using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>按标记颜色保存实际敌人实例；抽空时仅洗回该颜色的弃置标记。</summary>
    public sealed class EnemyTokenSupply
    {
        private readonly List<Monster> _draw = new();
        private readonly List<Monster> _discard = new();
        private readonly List<Monster> _inUse = new();
        private readonly List<(Monster Token, PlayerState Owner)> _puppets = new();
        private readonly Random _random;
        public IReadOnlyList<Monster> DrawPile => _draw;
        public IReadOnlyList<Monster> DiscardPile => _discard;
        public IReadOnlyList<Monster> InUse => _inUse;
        public IReadOnlyList<Monster> PuppetReservations => _puppets.Select(p => p.Token).ToArray();

        public EnemyTokenSupply(IEnumerable<Monster> tokens = null, Random random = null)
        {
            _random = random ?? new Random();
            foreach (var token in tokens ?? Array.Empty<Monster>())
            {
                if (token == null || token.TokenColor == EnemyTokenColor.Unknown || _draw.Any(t => ReferenceEquals(t, token)))
                    throw new ArgumentException("敌人供给必须使用颜色已核对且不重复的实际标记实例");
                _draw.Add(token);
            }
        }

        public bool CanDraw(EnemyTokenColor color) => color != EnemyTokenColor.Unknown
            && (_draw.Any(t => t.TokenColor == color) || _discard.Any(t => t.TokenColor == color));

        /// <summary>Peek the same top token a later Draw will take, without taking it out of supply.</summary>
        public Monster Peek(EnemyTokenColor color)
        {
            if (!CanDraw(color)) throw new InvalidOperationException("对应颜色的敌人标记供给已耗尽");
            if (!_draw.Any(t => t.TokenColor == color))
            {
                var recycled = _discard.Where(t => t.TokenColor == color).ToList();
                for (int i = recycled.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (recycled[i], recycled[j]) = (recycled[j], recycled[i]);
                }
                _discard.RemoveAll(t => t.TokenColor == color);
                _draw.AddRange(recycled);
            }
            return _draw.First(t => t.TokenColor == color);
        }

        public Monster Draw(EnemyTokenColor color)
        {
            var top = Peek(color);
            int index = _draw.FindIndex(t => ReferenceEquals(t, top));
            var result = _draw[index];
            _draw.RemoveAt(index);
            _inUse.Add(result);
            return result;
        }

        /// <summary>Draw one actual random token of a verified color, without creating a template copy.</summary>
        public Monster DrawRandom(EnemyTokenColor color)
        {
            Peek(color); // Recycle only this color when its draw pile is empty.
            var candidates = _draw.Where(t => t.TokenColor == color).ToArray();
            var token = candidates[_random.Next(candidates.Length)];
            _draw.RemoveAt(_draw.FindIndex(t => ReferenceEquals(t, token)));
            _inUse.Add(token);
            return token;
        }

        public bool IsInUse(Monster token) => _inUse.Any(t => ReferenceEquals(t, token));

        internal void ReservePuppet(Monster token, PlayerState owner)
        {
            int index = _inUse.FindIndex(t => ReferenceEquals(t, token));
            if (index < 0 || owner == null) throw new InvalidOperationException("只能保存此供给实际出战的敌人标记");
            _inUse.RemoveAt(index);
            _puppets.Add((token, owner));
        }

        internal void ReleasePuppet(Monster token, PlayerState owner)
        {
            int index = _puppets.FindIndex(p => ReferenceEquals(p.Token, token) && ReferenceEquals(p.Owner, owner));
            if (index < 0) throw new InvalidOperationException("傀儡标记不属于该玩家或已经归还");
            _puppets.RemoveAt(index);
            _discard.Add(token);
        }

        internal bool ContainsPhysicalToken(Monster token) => IsInUse(token)
            || _draw.Any(t => ReferenceEquals(t, token)) || _discard.Any(t => ReferenceEquals(t, token))
            || _puppets.Any(p => ReferenceEquals(p.Token, token));

        public void Discard(Monster token)
        {
            // The encounter releases its guard, but a defeated puppet candidate remains
            // outside the recyclable piles until its owner's end-turn choice is resolved.
            if (_puppets.Any(p => ReferenceEquals(p.Token, token))) return;
            int index = _inUse.FindIndex(t => ReferenceEquals(t, token));
            if (index < 0) throw new InvalidOperationException("不能弃置未从此供给抽取或已弃置的敌人标记");
            _inUse.RemoveAt(index);
            _discard.Add(token);
        }

        public static EnemyTokenColor GuardColor(PlaceType type) => type switch
        {
            PlaceType.Keep => EnemyTokenColor.Gray,
            PlaceType.MageTower => EnemyTokenColor.Purple,
            _ => throw new InvalidOperationException("此地点不能按单守军规则重新抽取")
        };
    }
}
