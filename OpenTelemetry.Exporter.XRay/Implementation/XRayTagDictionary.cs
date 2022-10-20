using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayTagDictionary
    {
        private static readonly KeyValuePair<string, object>[] Empty = Array.Empty<KeyValuePair<string, object>>();

        private KeyValuePair<string, object>[] _extra = Empty;
        private int _extraCount;

        public State Initialize(IEnumerable<KeyValuePair<string, object>> values, bool ignoreExtra = false)
        {
            foreach (var item in values)
            {
                AddOrReplace(item, ignoreExtra);
            }

            return new State(this);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        private void Append(in KeyValuePair<string, object> value)
        {
            if (_extraCount == _extra.Length)
                GrowBuffer(_extra.Length * 2);

            var index = _extraCount++;
            _extra[index] = value;
        }

        private void GrowBuffer(int desiredCapacity)
        {
            var newCapacity = Math.Max(desiredCapacity, 16);
            var newItems = ArrayPool<KeyValuePair<string, object>>.Shared.Rent(newCapacity);

            Array.Copy(_extra, newItems, _extraCount);

            ReturnBuffer();
            _extra = newItems;
        }

        private void ReturnBuffer()
        {
            if (!ReferenceEquals(_extra, Empty))
            {
                Array.Clear(_extra, 0, _extraCount);
                ArrayPool<KeyValuePair<string, object>>.Shared.Return(_extra);
            }
        }

        private void ResetBuffer()
        {
            ReturnBuffer();
            _extra = Empty;
            _extraCount = 0;
        }

        public partial struct State
        {
        }

        public partial struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private XRayTagDictionary _dictionary;
            private KeyValuePair<string, object> _current;
            private long _currentBits;
            private int _currentIndex;
            private int _next;
            private int _extraIndex;

            public readonly KeyValuePair<string, object> Current => _current;
            readonly object IEnumerator.Current => _current;

            public Enumerator(XRayTagDictionary dictionary)
            {
                _dictionary = dictionary;

                _current = default(KeyValuePair<string, object>);
                _currentBits = dictionary._bits0;
                _currentIndex = 0;
                _next = -1;
                _extraIndex = 0;

                GetNext();
            }

            public void Reset()
            {
                _current = default(KeyValuePair<string, object>);
                _currentBits = _dictionary._bits0;
                _currentIndex = 0;
                _next = -1;
                _extraIndex = 0;

                GetNext();
            }

            public void Dispose()
            {
            }
        }
    }
}