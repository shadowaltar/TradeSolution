using Common;

namespace TradeCommon.Tests.Common;

public class ObjectPoolTests
{
    public class TestPoolObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }

    [Test]
    public void Get10Objects_Good()
    {
        var pool = new Pool<TestPoolObject>();
        for (int i = 0; i < 10; i++)
        {
            var obj = pool.Lease();
            obj.Id = i.ToString();
            Assert.That(obj, Is.Null);
        }
    }

    [Test]
    public void Get1ObjectThenReturn_ExpectSameObject_Good()
    {
        var pool = new Pool<TestPoolObject>();
        var obj = pool.Lease();
        var id = obj.Id;
        pool.Return(obj);

        var obj2 = pool.Lease();
        Assert.That(id, Is.EqualTo(obj2.Id));
    }

    [Test]
    public void GetInitialCapacity()
    {
        var initialCount = 20;
        var pool = new Pool<TestPoolObject>(initialCount);
        Assert.That(initialCount, Is.EqualTo(pool.Count));
    }

    [Test]
    public void GetLeasedCount()
    {
        var initialCount = 20;
        var pool = new Pool<TestPoolObject>(initialCount);
        var obj1 = pool.Lease();
        var obj2 = pool.Lease();
        var obj3 = pool.Lease();
        Assert.That(3, Is.EqualTo(pool.LeasedCount));

        pool.Return(obj1);
        Assert.That(2, Is.EqualTo(pool.LeasedCount));

        pool.Return(obj2);
        Assert.That(1, Is.EqualTo(pool.LeasedCount));

        pool.Return(obj3);
        Assert.That(0, Is.EqualTo(pool.LeasedCount));
    }

    [Test]
    public void GetExpectedCount()
    {
        var initialCount = 2;
        var pool = new Pool<TestPoolObject>(initialCount);
        Assert.That(initialCount, Is.EqualTo(pool.ExpectedCount));

        var obj1 = pool.Lease();
        var obj2 = pool.Lease();
        var obj3 = pool.Lease();
        Assert.That(3, Is.EqualTo(pool.ExpectedCount));

        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3);
        Assert.That(3, Is.EqualTo(pool.ExpectedCount));
    }
}