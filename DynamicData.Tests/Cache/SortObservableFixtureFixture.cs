using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    
    public class SortObservableFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _cache;
        private readonly SortedChangeSetAggregator<Person, string> _results;        
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();
        private readonly BehaviorSubject<IComparer<Person>> _comparerObservable;
        private readonly SortExpressionComparer<Person> _comparer;

        //  private IComparer<Person> _comparer;

        public  SortObservableFixture()
        {
            _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
            _comparerObservable = new BehaviorSubject<IComparer<Person>>(_comparer);
            _cache = new SourceCache<Person, string>(p => p.Name);
          //  _sortController = new SortController<Person>(_comparer);

            _results = new SortedChangeSetAggregator<Person, string>
            (
                _cache.Connect().Sort(_comparerObservable, resetThreshold:25)
            );
        }

        public void Dispose()
        {
            _cache.Dispose();
            _results.Dispose();
            _comparerObservable.OnCompleted();
        }

        [Test]
        public void SortInitialBatch()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void ChangeSort()
        {
            var people = _generator.Take(100).ToArray();
            _cache.AddOrUpdate(people);

            var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

            _comparerObservable.OnNext(desc);
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[0].SortedItems.ToList();

            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }

        [Test]
        public void ChangeSortWithinThreshold()
        {
            var people = _generator.Take(20).ToArray();
            _cache.AddOrUpdate(people);

            var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

            _comparerObservable.OnNext(desc);
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var items = _results.Messages.Last().SortedItems;
            var actualResult = items.ToList();
            var sortReason = items.SortReason;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
            sortReason.Should().Be(SortReason.Reorder);
        }

        [Test]
        public void ChangeSortAboveThreshold()
        {
            var people = _generator.Take(30).ToArray();
            _cache.AddOrUpdate(people);

            var desc = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

            _comparerObservable.OnNext(desc);
            var expectedResult = people.OrderBy(p => p, desc).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var items = _results.Messages.Last().SortedItems;
            var actualResult = items.ToList();
            var sortReason = items.SortReason;
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
            sortReason.Should().Be(SortReason.Reset);
        }

        [Test]
        public void InlineChanges()
        {
            var people = _generator.Take(10000).ToArray();
            _cache.AddOrUpdate(people);

            //apply mutable changes to the items
            var random = new Random();
            var tochange = people.OrderBy(x => Guid.NewGuid()).Take(10).ToList();

            tochange.ForEach(p => p.Age = random.Next(0, 100));

            _cache.Refresh(tochange);

            var expected = people.OrderBy(t => t, _comparer).ToList();
            var actual = _results.Messages.Last().SortedItems.Select(kv => kv.Value).ToList();
            actual.ShouldAllBeEquivalentTo(expected);

            var list = new ObservableCollectionExtended<Person>();
            var adaptor = new SortedObservableCollectionAdaptor<Person, string>();
            foreach (var message in _results.Messages)
            {
                adaptor.Adapt(message, list);
            }
            list.ShouldAllBeEquivalentTo(expected);
        }

        [Test]
        public void Reset()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).OrderBy(x => Guid.NewGuid()).ToArray();
            _cache.AddOrUpdate(people);
            _comparerObservable.OnNext(SortExpressionComparer<Person>.Descending(p => p.Age));
            _comparerObservable.OnNext(_comparer);

            var expectedResult = people.OrderBy(p => p, _comparer).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
            var actualResult = _results.Messages[2].SortedItems.ToList();
            actualResult.ShouldAllBeEquivalentTo(expectedResult);
        }
    }
}