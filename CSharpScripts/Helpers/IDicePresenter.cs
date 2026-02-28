using System.Threading.Tasks;

public interface IDicePresenter
{
    Task PresentAsync(RollEvent rollEvent);
}
