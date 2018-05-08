// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Server.IntegrationTesting
{
    public class TestList : IEnumerable<object[]>
    {
        private readonly IList<TestVariant> _variants = new List<TestVariant>();

        public TestList()
        {
        }

        public TestList(IList<TestVariant> variants)
        {
            _variants = variants ?? throw new ArgumentNullException(nameof(variants));
        }

        public void Add(TestVariant variant) => _variants.Add(variant);

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var variant in _variants)
            {
                yield return new object[] { variant };
            }
        }

        IEnumerator<object[]> IEnumerable<object[]>.GetEnumerator()
        {
            foreach (var variant in _variants)
            {
                yield return new object[] { variant };
            }
        }

        public TestList Skip(Func<TestVariant, bool> check)
        {
            return new TestList(_variants.Where(v => !check(v)).ToList());
        }
    }
}
