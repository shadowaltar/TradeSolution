using TradeCommon.Essentials.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.Screening;

public class SingleSecurityLogic : ISecurityScreeningAlgoLogic
{
    private readonly Context _context;
    private Security? _security;
    private readonly Dictionary<int, Security> _securities = new(1);

    public SingleSecurityLogic(Context context, Security? security)
    {
        _context = context;
        _security = security;
        _securities = security != null ? new Dictionary<int, Security> { { security.Id, security } } : new Dictionary<int, Security>();
    }

    public void SetAndPick(IDictionary<int, Security> securityPool)
    {
        if (securityPool == null || securityPool.Count == 0) throw new ArgumentNullException(nameof(securityPool));

        _security = securityPool.Values.First();
        if (_security == null) throw new InvalidOperationException("Must provide at least one security in the pool for screening.");

        _securities.Clear();
        _securities[_security.Id] = _security;
    }

    public bool CheckIsPicked(int securityId)
    {
        return _security != null && securityId == _security?.Id;
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
