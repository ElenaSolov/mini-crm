import { useState } from 'react';

// Suggestions only, kept in the frontend. Any 3-letter code can still be typed.
const commonCurrencies = ['USD', 'EUR', 'GBP', 'ILS', 'JPY', 'CHF', 'CAD', 'AUD', 'CNY', 'INR', 'UAH', 'PLN'];

interface Props {
  selected: string[];
  onChange: (currencies: string[]) => void;
}

// Pick several currencies. The browser's <datalist> gives the autocomplete,
// so there is no extra library. Each picked code shows as a chip with a remove button.
export default function CurrencyFilter({ selected, onChange }: Readonly<Props>) {
  const [text, setText] = useState('');

  function add(value: string) {
    const code = value.trim().toUpperCase();
    if (/^[A-Z]{3}$/.test(code) && !selected.includes(code))
      onChange([...selected, code]);
    setText('');
  }

  // Choosing a suggestion fills the whole code at once, so add it right away.
  function handleInput(value: string) {
    if (commonCurrencies.includes(value.toUpperCase()))
      add(value);
    else
      setText(value);
  }

  // A div, not a <label>: clicking a label "clicks" the first control inside it,
  // which here would be the first chip's remove button.
  return (
    <div className="field">
      <span>Currency</span>
      <div className="multi-select">
        {selected.map((code) => (
          <span key={code} className="chip">
            {code}
            <button type="button" aria-label={`Remove ${code}`} onClick={() => onChange(selected.filter((c) => c !== code))}>
              ×
            </button>
          </span>
        ))}
        <input
          aria-label="Currency"
          list="currency-options"
          placeholder={selected.length === 0 ? 'e.g. USD' : ''}
          value={text}
          onChange={(e) => handleInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') add(text);
            if (e.key === 'Backspace' && text === '' && selected.length > 0) onChange(selected.slice(0, -1));
          }}
        />
      </div>
      <datalist id="currency-options">
        {commonCurrencies.filter((code) => !selected.includes(code)).map((code) => (
          <option key={code} value={code} />
        ))}
      </datalist>
    </div>
  );
}
