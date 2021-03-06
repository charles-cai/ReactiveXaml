﻿using ReactiveXaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ReactiveXaml.Tests
{
    public class ValidatedTestFixture : ReactiveValidatedObject
    {
        string _IsNotNullString;
        [Required]
        public string IsNotNullString {
            get { return _IsNotNullString; }
            set { this.RaiseAndSetIfChanged(x => x.IsNotNullString, value); }
        }
        
        string _IsOnlyOneWord;
        [RegularExpression(@"^[a-zA-Z]+$")]
        public string IsOnlyOneWord {
            get { return _IsOnlyOneWord; }
            set { this.RaiseAndSetIfChanged(x => x.IsOnlyOneWord, value); }
        }

        string _UsesExprRaiseSet;
        public string UsesExprRaiseSet {
            get { return _UsesExprRaiseSet; }
            set { _UsesExprRaiseSet = this.RaiseAndSetIfChanged(x => x.UsesExprRaiseSet, value); }
        }
    }

    [TestClass()]
    public class ReactiveValidatedObjectTest
    {
        [TestMethod()]
        public void IsValidTest()
        {
            return;
            var output = new List<bool>();
            var fixture = new ValidatedTestFixture();
            //fixture.IsValidObservable.Subscribe(output.Add);

            Assert.IsFalse(fixture.IsValid());

            fixture.IsNotNullString = "foo";
            Assert.IsFalse(fixture.IsValid());

            fixture.IsOnlyOneWord = "Foo Bar";
            Assert.IsFalse(fixture.IsValid());

            fixture.IsOnlyOneWord = "Foo";
            Assert.IsTrue(fixture.IsValid());

            fixture.IsOnlyOneWord = "";
            Assert.IsFalse(fixture.IsValid());

            new[] { false, false, false, true, false }.Zip(output, (expected, actual) => new { expected, actual })
                .Do(Console.WriteLine)
                .Run(x => Assert.AreEqual(x.expected, x.actual));
        }
    }
}
