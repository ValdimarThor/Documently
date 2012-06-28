using System.Collections.Generic;
using System.Diagnostics;
using Documently.ReadModel;

namespace Documently.WebApp.Handlers.DocumentList
{
	public class DocumentListModel
	{
		readonly IReadRepository _repository;

		public DocumentListModel(IReadRepository repository)
		{
			_repository = repository;
		}

		public IEnumerable<DocumentMetaListDto> Documents
		{
			get
			{
				var docs = _repository.GetAll<DocumentMetaListDto>();
				return docs;
			}
		}
	}
}