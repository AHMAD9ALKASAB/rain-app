(function () {
  const root = document.querySelector('[data-chatbot-root]');
  if (!root) return;
  const thread = root.querySelector('[data-chatbot-thread]');
  const form = root.querySelector('[data-chatbot-form]');
  const input = root.querySelector('[data-chatbot-input]');
  const quickButtons = root.querySelectorAll('[data-chatbot-question]');
  const endpoint = '/api/chatbot';
  const history = [];

  const fallback = {
    ar: 'حدث تعذّر مؤقت في جلب الرد، يمكنك إعادة المحاولة أو استخدام نموذج التواصل.',
    en: 'We could not reach the assistant right now. Please try again or use the contact form.'
  };

  function detectLanguage() {
    const html = document.documentElement;
    return html.lang && html.lang.startsWith('ar') ? 'ar' : 'en';
  }

  function addBubble(text, type, meta) {
    const bubble = document.createElement('div');
    bubble.className = `chatbot-bubble chatbot-bubble--${type}`;
    bubble.textContent = text;
    if (meta?.source) {
      bubble.dataset.source = meta.source;
    }
    thread.appendChild(bubble);
    thread.scrollTo({ top: thread.scrollHeight, behavior: 'smooth' });
    emitAnalytics('bubble', { type, source: meta?.source });
  }

  function setLoading(state) {
    if (state) {
      root.classList.add('chatbot-loading');
    } else {
      root.classList.remove('chatbot-loading');
    }
  }

  function emitAnalytics(action, detail) {
    document.dispatchEvent(new CustomEvent('chatbot:analytics', {
      detail: {
        action,
        timestamp: Date.now(),
        ...detail
      }
    }));
  }

  async function fetchReply(message) {
    const res = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify({
        message,
        history: history.map(item => ({
          role: item.role,
          content: item.content
        }))
      })
    });
    if (!res.ok) {
      throw new Error(`Chatbot failed with status ${res.status}`);
    }
    return await res.json();
  }

  async function handleSubmit(message) {
    if (!message.trim()) {
      return;
    }
    addBubble(message, 'user');
    history.push({ role: 'user', content: message });
    emitAnalytics('question', { message });
    input.value = '';
    setLoading(true);
    try {
      const data = await fetchReply(message);
      addBubble(data.message, 'bot', { source: data.source });
      history.push({ role: 'assistant', content: data.message });
      emitAnalytics('answer', { source: data.source, provider: data.fromProvider });
    } catch (err) {
      console.error(err);
      const lang = detectLanguage();
      addBubble(fallback[lang], 'bot', { source: 'client-fallback' });
      emitAnalytics('error', { message: err?.message });
    } finally {
      setLoading(false);
    }
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    const value = input.value;
    handleSubmit(value);
  });

  quickButtons.forEach(btn => {
    btn.addEventListener('click', function () {
      const question = btn.getAttribute('data-chatbot-question') || '';
      handleSubmit(question);
    });
  });
})();
