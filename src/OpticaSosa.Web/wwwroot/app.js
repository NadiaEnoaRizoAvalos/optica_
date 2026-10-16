(() => {
    const files = document.querySelector('[data-clinical-files]');
    if (files) {
        const container = document.querySelector('[data-file-previews]');
        let selected = [], urls = [];
        const render = () => {
            urls.forEach(url => URL.revokeObjectURL(url)); urls = []; container.replaceChildren();
            const transfer = new DataTransfer(); selected.forEach(file => transfer.items.add(file)); files.files = transfer.files;
            selected.forEach((file, index) => {
                const card = document.createElement('article'); card.className = 'file-preview';
                const text = document.createElement('p'); text.textContent = `${file.name} · ${(file.size / 1048576).toFixed(2)} MiB`; card.append(text);
                const url = URL.createObjectURL(file); urls.push(url);
                if (['image/jpeg', 'image/png'].includes(file.type)) { const img = document.createElement('img'); img.src = url; img.alt = 'Vista previa local del archivo seleccionado'; card.append(img); }
                // Los PDF no se insertan ni ejecutan dentro de la aplicación.
                const link = document.createElement('a'); link.href = url; link.textContent = 'Abrir vista local'; link.target = '_blank'; link.rel = 'noopener noreferrer'; card.append(link);
                const remove = document.createElement('button'); remove.type = 'button'; remove.textContent = 'Quitar de la selección';
                remove.onclick = () => { selected.splice(index, 1); render(); }; card.append(remove); container.append(card);
            });
        };
        files.addEventListener('change', () => { selected.push(...files.files); render(); });
        window.addEventListener('pagehide', () => urls.forEach(url => URL.revokeObjectURL(url)));
    }
    const search = document.querySelector('[data-search-professionals]');
    const clinicalForm = document.querySelector('[data-clinical-editor]');
    if (clinicalForm) {
        const type = clinicalForm.querySelector('[data-record-type]');
        const update = () => {
            const history = type ? ['history', 'followup'].includes(type.value) : clinicalForm.elements.namedItem('Input.Kind')?.value === 'VisualHistory';
            clinicalForm.querySelectorAll('[data-graduations] input').forEach(input => input.disabled = history);
            const box = clinicalForm.querySelector('[data-graduations]'); if (box) box.hidden = history;
        };
        type?.addEventListener('change', update); update();
    }
    search?.addEventListener('click', async event => {
        event.preventDefault(); search.disabled = true;
        try {
            const form = search.form, payload = new FormData();
            for (const key of ['__RequestVerificationToken', 'ProfessionalSearch', 'Input.ProfessionalId']) payload.set(key, form.elements.namedItem(key)?.value ?? '');
            const response = await fetch(search.formAction, { method: 'POST', body: payload, credentials: 'same-origin' });
            if (!response.ok) throw new Error();
            const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
            const options = doc.querySelector('#Input_ProfessionalId'); if (!options) throw new Error();
            document.querySelector('#Input_ProfessionalId').replaceChildren(...options.children);
        } catch { alert('No se pudo actualizar la búsqueda. Conservá los datos y archivos seleccionados e intentá nuevamente.'); }
        finally { search.disabled = false; }
    });
    const form = document.querySelector('[data-quality-form]');
    if (form) {
        const validate = () => {
            let valid = true;
            form.querySelectorAll('.quality-check').forEach(row => {
                const status = row.querySelector('[data-check-status]').value, notes = row.querySelector('[data-check-notes]');
                const applicable = row.dataset.applicable === 'true', required = row.dataset.required === 'true';
                if (status === '2' || (applicable ? status === '3' || required && status !== '1' : status !== '3' || !notes.value.trim())) valid = false;
            });
            if (form.dataset.rectifying === 'true' && !form.querySelector('[data-control-notes]').value.trim()) valid = false;
            const approve = form.querySelector('[data-approve]'); if (approve) approve.disabled = !valid;
            form.querySelector('[data-approval-hint]').textContent = valid ? 'Checklist completo. Podés solicitar la aprobación; el servidor validará permisos y vigencia.' : 'Completá los ítems obligatorios sin observaciones y justificá los No aplica para aprobar.';
        };
        form.addEventListener('input', validate); form.addEventListener('change', validate); validate();
    }
})();
