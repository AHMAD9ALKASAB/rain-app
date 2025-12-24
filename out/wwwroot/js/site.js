// Theme toggle (light/dark) with localStorage, prefers-color-scheme fallback
(function(){
  const key = 'rain-theme';
  const root = document.documentElement;
  function apply(t){ root.setAttribute('data-theme', t); }
  function current(){ return root.getAttribute('data-theme'); }
  let saved = localStorage.getItem(key);
  if(!saved){
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    saved = prefersDark ? 'dark' : 'light';
    localStorage.setItem(key, saved);
  }
  apply(saved);
  const btn = document.getElementById('themeToggle');
  if(btn){
    btn.addEventListener('click', function(){
      const next = current() === 'dark' ? 'light' : 'dark';
      apply(next);
      localStorage.setItem(key, next);
    });
  }
  // Hide external login column on Identity pages when no providers desired
  const p = location.pathname.toLowerCase();
  if (p.startsWith('/identity/account/login') || p.startsWith('/identity/account/register')) {
    document.body.classList.add('no-external');
  }
  if (p === '/' || p === '' || p === '/home' || p.startsWith('/home/index')) {
    document.body.classList.add('home-gradient');
  }

  // Scroll reveal
  const els = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && els.length){
    const obs = new IntersectionObserver((entries)=>{
      for(const e of entries){
        if(e.isIntersecting){
          e.target.classList.add('reveal--visible');
          obs.unobserve(e.target);
        }
      }
    }, { threshold: 0.15 });
    els.forEach(el=>obs.observe(el));
  } else {
    els.forEach(el=>el.classList.add('reveal--visible'));
  }

  // Interactive 3D cube (mouse driven)
  const cube = document.getElementById('rainCube');
  const scene = cube ? cube.closest('.cube-scene') : null;
  if (cube && scene) {
    let rafId = null;
    function handle(x, y){
      const rect = scene.getBoundingClientRect();
      const cx = rect.left + rect.width/2;
      const cy = rect.top + rect.height/2;
      const dx = (x - cx) / (rect.width/2);   // -1..1
      const dy = (y - cy) / (rect.height/2);  // -1..1
      const rotY = dx * 180; // full 360 sweep
      const rotX = -dy * 60 - 20; // subtle tilt plus baseline
      cube.classList.add('cube--interactive');
      cube.style.transform = `rotateX(${rotX}deg) rotateY(${rotY}deg)`;
    }
    function onMove(e){
      const x = e.touches ? e.touches[0].clientX : e.clientX;
      const y = e.touches ? e.touches[0].clientY : e.clientY;
      if (rafId) cancelAnimationFrame(rafId);
      rafId = requestAnimationFrame(()=>handle(x,y));
    }
    scene.addEventListener('mousemove', onMove);
    scene.addEventListener('touchmove', onMove, { passive: true });
    scene.addEventListener('mouseleave', ()=>{
      cube.classList.remove('cube--interactive');
      cube.style.transform = '';
    });
  }

  // Hero animated background (subtle moving blobs)
  const canvas = document.getElementById('heroBg');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    function resize(){ canvas.width = canvas.clientWidth; canvas.height = canvas.clientHeight; }
    resize();
    window.addEventListener('resize', resize);
    const blobs = Array.from({length: 6}).map((_,i)=>({
      x: Math.random()*canvas.width,
      y: Math.random()*canvas.height,
      r: 60 + Math.random()*90,
      dx: (Math.random()-.5)*0.4,
      dy: (Math.random()-.5)*0.4,
      c: i%2===0 ? 'rgba(124,77,255,0.15)' : 'rgba(77,217,255,0.12)'
    }));
    function tick(){
      ctx.clearRect(0,0,canvas.width,canvas.height);
      for(const b of blobs){
        b.x+=b.dx; b.y+=b.dy;
        if(b.x-b.r<0||b.x+b.r>canvas.width) b.dx*=-1;
        if(b.y-b.r<0||b.y+b.r>canvas.height) b.dy*=-1;
        const g = ctx.createRadialGradient(b.x,b.y,10,b.x,b.y,b.r);
        g.addColorStop(0,b.c); g.addColorStop(1,'rgba(0,0,0,0)');
        ctx.fillStyle=g; ctx.beginPath(); ctx.arc(b.x,b.y,b.r,0,Math.PI*2); ctx.fill();
      }
      requestAnimationFrame(tick);
    }
    tick();
  }
})();
