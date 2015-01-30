using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class TrueForAllFixture
    {
        private ISourceCache<ObjectWithObservable, int> _source;
        private IObservable<bool> _observable;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<ObjectWithObservable, int>(p => p.Id);
            _observable = _source.Connect().TrueForAll(o => o.Observable.StartWith(o.Value), o => o == true);

        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void InitialItemReturnsFalseWhenObservaleHasNoValue()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result =>
            {
                valuereturned = result;
            });

            var item = new ObjectWithObservable(1);
            _source.AddOrUpdate(item);

            Assert.IsTrue(valuereturned.HasValue, "An intial value should have been called");
            Assert.AreEqual(false, valuereturned.Value, "The intial value should be false");
   
            subscribed.Dispose();
        }

        [Test]
        public void InlineObservableChangeProducesResult()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result =>
            {
                valuereturned = result;
            });

            var item = new ObjectWithObservable(1);
            item.InvokeObservable(true);
            _source.AddOrUpdate(item);
         
            Assert.AreEqual(true, valuereturned.Value, "Value should be true");
            subscribed.Dispose();
        }

        [Test]
        public void MultipleValuesReturnTrue()
        {
            bool? valuereturned = null;
            var subscribed = _observable.Subscribe(result =>
            {
                valuereturned = result;
            });

            var item1 = new ObjectWithObservable(1);
            var item2 = new ObjectWithObservable(2);
            var item3 = new ObjectWithObservable(3);
            _source.AddOrUpdate(item1);
            _source.AddOrUpdate(item2);
            _source.AddOrUpdate(item3);
            Assert.AreEqual(false, valuereturned.Value, "Value should be false");

            item3.InvokeObservable(true);
            Assert.AreEqual(false, valuereturned.Value, "Value should be true");
            subscribed.Dispose();
        }


    private class ObjectWithObservable
        {
            private readonly int _id;
            private readonly ISubject<bool> _changed = new Subject<bool>();
            private bool _value;

            public ObjectWithObservable(int id)
            {
                _id = id;
            }

            public void InvokeObservable(bool value)
            {
                _value = value;
                _changed.OnNext(value);
            }

            public IObservable<bool> Observable
            {
                get { return _changed; }
            }

        public bool Value
        {
            get { return _value; }
        }

            public int Id
            {
                get { return _id; }
            }
        }

    }
}