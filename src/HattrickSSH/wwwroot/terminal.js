(() => {
  const term = new Terminal({
    cursorBlink: true, convertEol: false, scrollback: 10000,
    fontFamily: 'Cascadia Code, Consolas, "Courier New", monospace',
    fontSize: 16, lineHeight: 1.18, allowProposedApi: true,
    theme: { background:'#05080c', foreground:'#e6edf3', cursor:'#e6edf3',
      cursorAccent:'#05080c', selectionBackground:'#24533e',
      black:'#080b10', red:'#ff6b75', green:'#35e58f', yellow:'#e8c878',
      blue:'#71a7ff', magenta:'#c792ea', cyan:'#6edddd', white:'#dce5ee',
      brightBlack:'#657487', brightRed:'#ff8790', brightGreen:'#64f5a8',
      brightYellow:'#f5d98d', brightBlue:'#8dbaff', brightMagenta:'#d8aaff',
      brightCyan:'#89eeee', brightWhite:'#ffffff' }
  });
  const fit = new FitAddon.FitAddon();
  term.loadAddon(fit);
  term.open(document.getElementById('terminal'));
  const send = message => window.chrome?.webview?.postMessage(message);
  let resizePending = 0;
  const fitAndResize = () => {
    try { fit.fit(); send({type:'resize',cols:term.cols,rows:term.rows}); } catch {}
  };
  term.onData(data => send({type:'input',data}));
  term.onResize(size => send({type:'resize',cols:size.cols,rows:size.rows}));
  window.chrome?.webview?.addEventListener('message', event => {
    const payload = event.data;
    if (typeof payload !== 'string') return;
    try {
      const bytes = Uint8Array.from(atob(payload), c => c.charCodeAt(0));
      term.write(new TextDecoder('utf-8', {fatal:false}).decode(bytes, {stream:true}));
    } catch {}
  });
  window.hsshWrite = text => term.write(text);
  window.hsshReset = () => term.reset();
  window.hsshSetFontSize = size => { term.options.fontSize = Math.max(10,Math.min(30,Number(size)||16)); fitAndResize(); };
  window.hsshSetTheme = palette => { term.options.theme = {...term.options.theme, ...palette}; };
  window.hsshSetScrollback = rows => { term.options.scrollback = Math.max(500,Math.min(100000,Number(rows)||10000)); };
  window.hsshClear = () => term.clear();
  window.hsshFit = fitAndResize;
  const observer = new ResizeObserver(() => { clearTimeout(resizePending); resizePending=setTimeout(fitAndResize,80); });
  observer.observe(document.getElementById('terminal'));
  window.addEventListener('resize',fitAndResize);
  setTimeout(fitAndResize,100);
  term.focus();
})();