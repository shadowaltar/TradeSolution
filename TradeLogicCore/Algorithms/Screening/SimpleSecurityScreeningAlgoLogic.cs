using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
<<<<<<< HEAD
    private static readonly List<Security> _empty = new();
    private static readonly List<Security> _originalPool = new();
    private static readonly Dictionary<int, Security> _pickedPool = new();

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public IReadOnlyCollection<Security> GetPickedOnes(List<Security> securityPool)
    {
        lock (_pickedPool)
            return _pickedPool.Values;
    }

    public void Pick(List<Security> securityPool)
=======
    private readonly List<Security> _originalPool = new();
    private readonly Dictionary<int, Security> _pickedPool = new();

    public void SetAndPick(List<Security> securityPool)
>>>>>>> 76ee123a3f052a2e2cab3966024a518b20502019
    {
        lock (_originalPool)
        {
            _originalPool.Clear();
            _originalPool.AddRange(securityPool);
            lock (_pickedPool)
            {
                _pickedPool.Clear();
                foreach (var security in _originalPool)
                {
                    _pickedPool[security.Id] = security;
                }
            }
        }
    }
<<<<<<< HEAD
=======

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public IReadOnlyCollection<Security> GetPickedOnes()
    {
        lock (_pickedPool)
            return _pickedPool.Values;
    }

    public void Repick()
    {
    }
>>>>>>> 76ee123a3f052a2e2cab3966024a518b20502019
}

public class SingleSecurityLogic : ISecurityScreeningAlgoLogic
{
    private Security? _security;
<<<<<<< HEAD
    private List<Security> _securities;
    public SingleSecurityLogic(Security? security)
    {
        _security = security;
        if (security != null)
            _securities = new List<Security> { security };
        else
            _securities = new List<Security>();
=======
    private readonly List<Security> _securities = new(1);

    public void SetAndPick(List<Security> securityPool)
    {
        if (securityPool == null || securityPool.Count == 0) throw new ArgumentNullException(nameof(securityPool));

        _security = securityPool[0];
        if (_security == null) throw new InvalidOperationException("Must provide at least one security in the pool for screening.");

        _securities.Clear();
        _securities.Add(_security);
>>>>>>> 76ee123a3f052a2e2cab3966024a518b20502019
    }

    public bool CheckIsPicked(int securityId)
    {
        if (_security == null) return false;
        return securityId == _security?.Id;
    }

<<<<<<< HEAD
    public IReadOnlyCollection<Security> GetPickedOnes(List<Security> securityPool)
=======
    public IReadOnlyCollection<Security> GetPickedOnes()
>>>>>>> 76ee123a3f052a2e2cab3966024a518b20502019
    {
        return _securities;
    }

<<<<<<< HEAD
    public void Pick(List<Security> securityPool)
=======
    public void Repick()
>>>>>>> 76ee123a3f052a2e2cab3966024a518b20502019
    {
    }
}
