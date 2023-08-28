using Common;
using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SimpleSecurityScreeningAlgoLogic : ISecurityScreeningAlgoLogic
{
    private static readonly Dictionary<int, Security> _originalPool = new();
    private static readonly Dictionary<int, Security> _pickedPool = new();

    public bool CheckIsPicked(int securityId)
    {
        lock (_pickedPool)
        {
            return _pickedPool.ContainsKey(securityId);
        }
    }

    public void SetAndPick(IDictionary<int, Security> securityPool)
    {
        lock (_originalPool)
        {
            _originalPool.Clear();
            _originalPool.AddRange(securityPool);
            lock (_pickedPool)
            {
                _pickedPool.Clear();
                foreach (var pair in _originalPool)
                {
                    _pickedPool[pair.Key] = pair.Value;
                }
            }
        }
    }

    public IReadOnlyDictionary<int, Security> GetPickedOnes()
    {
        lock (_pickedPool)
            return _pickedPool;
    }

    public void Repick()
    {
    }

    public IReadOnlyDictionary<int, Security> GetAll()
    {
        lock (_originalPool)
            return _originalPool;
    }

    public bool TryCheckIfChanged(out IReadOnlyDictionary<int, Security> pickedOnes)
    {
        lock (_originalPool)
        {
            pickedOnes = _originalPool;
        }
        return false;
    }
}

public class SingleSecurityLogic : ISecurityScreeningAlgoLogic
{
    private Security? _security;
    private readonly Dictionary<int, Security> _securities = new(1);

    public SingleSecurityLogic(Security? security)
    {
        _security = security;
        if (security != null)
            _securities = new Dictionary<int, Security> { { security.Id, security } };
        else
            _securities = new Dictionary<int, Security>();
    }

    public void SetAndPick(IDictionary<int, Security> securityPool)
    {
        if (securityPool == null || securityPool.Count == 0) throw new ArgumentNullException(nameof(securityPool));

        _security = securityPool[0];
        if (_security == null) throw new InvalidOperationException("Must provide at least one security in the pool for screening.");

        _securities.Clear();
        _securities[_security.Id] = _security;
    }

    public bool CheckIsPicked(int securityId)
    {
        if (_security == null) return false;
        return securityId == _security?.Id;
    }

    public IReadOnlyDictionary<int, Security> GetPickedOnes()
    {
        return _securities;
    }

    public IReadOnlyDictionary<int, Security> GetAll()
    {
        return _securities;
    }

    public bool TryCheckIfChanged(out IReadOnlyDictionary<int, Security> pickedOnes)
    {
        pickedOnes = _securities;
        return false;
    }

    public void Repick()
    {
    }
}
