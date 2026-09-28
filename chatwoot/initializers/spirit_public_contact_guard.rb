# The public Client API must not take an email, phone number, or identifier from a browser.
#
# Anyone with the inbox identifier can call it, and the identifier is public. Given a stranger's
# email or phone, Chatwoot attaches the caller to the stranger's contact: on create through
# ContactInboxWithContactBuilder#find_contact, on update through ContactIdentifyAction. The answer
# then carries the stranger's name, email, and phone. Proved against 4.18.0 on 2026-09-24.
#
# Spirit's widget sends only a source_id, and Spirit sets phone and email with the staff API,
# which never merges. So the public contact routes keep the name and drop the rest.
#
# The /api/v1/widget routes merge the same way, but they need a Website inbox. We have none.
# Add one and they need the same guard.
Rails.application.config.to_prepare do
  controller = Public::Api::V1::Inboxes::ContactsController

  unless controller.private_method_defined?(:permitted_params)
    raise 'Spirit contact guard: Public::Api::V1::Inboxes::ContactsController#permitted_params ' \
          'is gone. Chatwoot changed. Fix chatwoot/initializers/spirit_public_contact_guard.rb.'
  end

  controller.class_eval do
    private

    def permitted_params
      params.permit(:name)
    end
  end
end
