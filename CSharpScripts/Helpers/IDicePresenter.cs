using System.Threading.Tasks;

public interface IDicePresenter
{
    int ActivePlayerTeamId { get; set; }
    Task PresentAsync(RollEvent rollEvent);
    Task<bool> WaitForRushAsync();
}
