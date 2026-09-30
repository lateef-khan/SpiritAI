# Lets the Spirit Hub frame Desk, and sends anyone who is not signed in to the Hub.
#
# SPIRIT_HUB_ORIGIN is the Hub's origin, for example https://hub.spiritfitnessapps.com.
# SPIRIT_HUB_LOGIN_URL is where a plain GET /app/login goes when it carries no sso_auth_token.
# /app/login?local=1 still shows Chatwoot's own form, for the back-door admin.
#
# A path is matched after collapsing repeated slashes and trimming one trailing slash, so
# /app/login, //app/login, and /app/login/ are the same route. Without that, a Spirit reviewer
# found paths like these slip past a plain string match.
class SpiritHub
  SIGN_OUT_PAGE = <<~HTML.freeze
    <!doctype html>
    <html><head><meta charset="utf-8"><title>Signing out</title></head>
    <body><script>
    (async () => {
      const hub = %<hub>s;
      let status = 'no-session';
      try {
        const match = document.cookie.split('; ').find(c => c.startsWith('cw_d_session_info='));
        if (match) {
          const info = JSON.parse(decodeURIComponent(match.split('=').slice(1).join('=')));
          const response = await fetch('/auth/sign_out', {
            method: 'DELETE',
            headers: { 'access-token': info['access-token'], client: info.client, uid: info.uid },
          });
          status = 'signed-out-' + response.status;
        }
      } finally {
        document.cookie = 'cw_d_session_info=; path=/; max-age=0; samesite=lax';
        if (window.parent !== window) window.parent.postMessage({ type: 'hub:signed-out', app: 'desk' }, hub);
        document.body.textContent = status;
      }
    })();
    </script></body></html>
  HTML

  def initialize(app)
    @app = app
    @hub_origin = ENV.fetch('SPIRIT_HUB_ORIGIN')
    @login_url = ENV.fetch('SPIRIT_HUB_LOGIN_URL')
  end

  def call(env)
    request = Rack::Request.new(env)
    path = normalized_path(request.path)
    return sign_out_page if request.get? && path == '/spirit/sign-out'
    return [302, { 'location' => @login_url, 'cache-control' => 'no-store' }, []] if hub_login?(request, path)

    status, headers, body = @app.call(env)
    allow_hub_frame(headers)
    [status, headers, body]
  end

  private

  # Collapses repeated slashes and drops one trailing slash, so //app/login and /app/login/
  # match the same route as /app/login.
  def normalized_path(path)
    path.gsub(%r{/{2,}}, '/').sub(%r{(.)/\z}, '\1')
  end

  def frame_ancestors
    "frame-ancestors 'self' #{@hub_origin}"
  end

  def hub_login?(request, path)
    request.get? && path == '/app/login' &&
      request.params['sso_auth_token'].blank? && request.params['local'] != '1'
  end

  def sign_out_page
    headers = { 'content-type' => 'text/html; charset=utf-8', 'cache-control' => 'no-store',
                'content-security-policy' => frame_ancestors }
    [200, headers, [format(SIGN_OUT_PAGE, hub: @hub_origin.to_json)]]
  end

  # A response that already carries its own content-security-policy is left exactly as Chatwoot
  # made it: a web widget inbox with allowed_domains sets frame-ancestors for its own domains and
  # keeps X-Frame-Options for older browsers, and a browser that understands frame-ancestors
  # ignores X-Frame-Options once it is present, so that inbox's framing still works unchanged.
  # Only a response with no content-security-policy of its own, but a bare X-Frame-Options
  # refusing every frame, gets ours instead.
  def allow_hub_frame(headers)
    return if headers['content-security-policy'] || headers['Content-Security-Policy']
    return unless headers.delete('x-frame-options') || headers.delete('X-Frame-Options')

    headers['content-security-policy'] = frame_ancestors
  end
end

Rails.application.config.middleware.insert_before 0, SpiritHub
