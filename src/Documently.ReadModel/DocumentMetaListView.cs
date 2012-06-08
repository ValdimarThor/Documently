using Documently.Messages;
using Documently.Messages.CustEvents;
using Documently.Messages.DocMetaEvents;
using Raven.Client;

namespace Documently.ReadModel
{
	public class DocumentMetaListView : HandlesEvent<Created>
	{
		private readonly IDocumentStore _DocumentStore;

		public DocumentMetaListView(IDocumentStore documentStore)
		{
			_DocumentStore = documentStore;
		}

		public void Consume(Created evt)
		{
			using (var session = _DocumentStore.OpenSession())
			{
				var documentMetaListDto = new DocumentMetaListDto()
				{
					AggregateId = evt.AggregateId,
					Title = evt.Title,
					UtcDate = evt.UtcDate
				};
				session.Store(documentMetaListDto);
				session.SaveChanges();
			}
		}
	}
}