using Documently.ReadModel;

namespace Documently.WebApp.Handlers.DocumentList
{
    public class GetHandler
    {
    	readonly IReadRepository _repository;

    	public GetHandler(IReadRepository repository)
    	{
    		_repository = repository;
    	}

    	public DocumentListModel Execute()
        {
			return new DocumentListModel(_repository);
        }
    }
}