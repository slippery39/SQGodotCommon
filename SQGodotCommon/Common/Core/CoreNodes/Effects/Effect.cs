using DeepCloneable;

namespace CoreNodes;

/// <summary>
/// Should an effect be an identifiable component?
/// </summary>
public abstract class Effect<TEffectContext> : CoreNode, IDeepCloneable<Effect<TEffectContext>>
{
	public virtual TargetInfo<TEffectContext> TargetInfo { get; set; }
	public TargetMode TargetMode => TargetInfo.TargetMode;
	public TargetSpecification<TEffectContext> TargetFilter => TargetInfo.Specification;

	public bool NeedsTargets() => TargetInfo.NeedsTargets();

	public abstract void Apply(TEffectContext context);

	public override Effect<TEffectContext> DeepClone()
	{
		var newEffect = base.DeepClone() as Effect<TEffectContext>;
		newEffect.TargetInfo = TargetInfo.DeepClone();
		return newEffect;
	}
}
