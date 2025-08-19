namespace CoreNodes;

public interface ICoreAction<in TContext>
{
	public abstract bool CanExecute(TContext battleState);
	public abstract void Execute(TContext battleState);
}
