# Pasul 2: transforma esantionul CSV intr-o baza SQLite normalizata, cu denumiri in romana
# Rulare: python 02_creare_baza.py lending_club_sample.csv credite_banca.db
import os, sqlite3, sys
import pandas as pd

sursa, destinatie = sys.argv[1], sys.argv[2]
d = pd.read_csv(sursa)
d = d[d.annual_inc.notna()].copy()

def luna(text):  # 'Dec-2018' -> '2018-12-01'
    return None if pd.isna(text) else pd.to_datetime(text, format='%b-%Y').strftime('%Y-%m-%d')

d['data_acordarii'] = d.issue_d.map(luna)
d['data_ultimei_plati'] = d.last_pymnt_d.map(luna)
d['durata_luni'] = d.term.str.strip().str.split().str[0].astype(int)   # ' 36 months' -> 36
d = d.sort_values('data_acordarii').reset_index(drop=True)
d['id_client'] = ['CL%06d' % (i + 1) for i in range(len(d))]          # dataset-ul original e anonimizat

TIPURI = {
    'debt_consolidation': 'Consolidare datorii', 'credit_card': 'Refinantare card de credit',
    'home_improvement': 'Renovare locuinta', 'other': 'Alte nevoi personale',
    'major_purchase': 'Achizitii majore (consum)', 'medical': 'Cheltuieli medicale',
    'small_business': 'Afaceri mici', 'car': 'Auto', 'vacation': 'Vacanta', 'moving': 'Mutare',
    'house': 'Locuinta', 'wedding': 'Nunta', 'renewable_energy': 'Energie regenerabila',
    'educational': 'Educatie',
}
# cod original, denumire, rau_platnic (0/1), gravitate (0 = ok ... 4 = pierdut), inchis (0/1)
STARI = [
    ('Fully Paid', 'Rambursat integral', 0, 0, 1),
    ('Current', 'Activ - la zi', 0, 0, 0),
    ('In Grace Period', 'In perioada de gratie', 1, 1, 0),
    ('Late (16-30 days)', 'Restant 16-30 zile', 1, 2, 0),
    ('Late (31-120 days)', 'Restant 31-120 zile', 1, 3, 0),
    ('Default', 'In incapacitate de plata', 1, 4, 0),
    ('Charged Off', 'Pierdut (trecut pe pierderi)', 1, 4, 1),
    ('Does not meet the credit policy. Status:Fully Paid', 'Rambursat integral (in afara politicii)', 0, 0, 1),
    ('Does not meet the credit policy. Status:Charged Off', 'Pierdut (in afara politicii)', 1, 4, 1),
]

if os.path.exists(destinatie):
    os.remove(destinatie)
db = sqlite3.connect(destinatie)
db.executescript("""
CREATE TABLE TipuriCredit(id_tip INTEGER PRIMARY KEY, cod TEXT UNIQUE NOT NULL, denumire TEXT NOT NULL);
CREATE TABLE StariCredit(id_stare INTEGER PRIMARY KEY, cod TEXT UNIQUE NOT NULL, denumire TEXT NOT NULL,
  rau_platnic INTEGER NOT NULL, gravitate INTEGER NOT NULL, inchis INTEGER NOT NULL);
CREATE TABLE Clienti(
  id_client TEXT PRIMARY KEY, stat TEXT, venit_anual REAL, vechime_munca TEXT, tip_locuinta TEXT,
  grad_indatorare REAL,      -- dti: rate lunare / venit lunar (%)
  utilizare_revolving REAL,  -- cat % din limita cardurilor foloseste
  nr_carduri INTEGER,        -- numar de carduri bancare detinute
  nr_ipoteci INTEGER,        -- numar de credite ipotecare
  conturi_deschise INTEGER,
  intarzieri_2ani INTEGER    -- intarzieri de peste 30 zile in ultimii 2 ani
);
CREATE TABLE Credite(
  id_credit INTEGER PRIMARY KEY,
  id_client TEXT NOT NULL REFERENCES Clienti(id_client),
  id_tip INTEGER NOT NULL REFERENCES TipuriCredit(id_tip),
  id_stare INTEGER NOT NULL REFERENCES StariCredit(id_stare),
  suma REAL NOT NULL, suma_finantata REAL NOT NULL, dobanda REAL NOT NULL, durata_luni INTEGER NOT NULL,
  rata_lunara REAL NOT NULL, grad_risc TEXT NOT NULL, data_acordarii TEXT NOT NULL,
  total_platit REAL, principal_rambursat REAL, dobanda_incasata REAL, penalitati REAL,
  recuperari REAL, cost_recuperare REAL, sold_ramas REAL, data_ultimei_plati TEXT
);
CREATE INDEX ix_cr_data ON Credite(data_acordarii);
CREATE INDEX ix_cr_tip ON Credite(id_tip);
CREATE INDEX ix_cr_stare ON Credite(id_stare);
CREATE INDEX ix_cr_client ON Credite(id_client);
""")
db.executemany('INSERT INTO TipuriCredit(cod, denumire) VALUES (?, ?)', TIPURI.items())
db.executemany('INSERT INTO StariCredit(cod, denumire, rau_platnic, gravitate, inchis) VALUES (?, ?, ?, ?, ?)', STARI)
id_tip = dict(db.execute('SELECT cod, id_tip FROM TipuriCredit'))
id_stare = dict(db.execute('SELECT cod, id_stare FROM StariCredit'))

def val(x):  # NaN -> NULL
    return None if pd.isna(x) else x

db.executemany('INSERT INTO Clienti VALUES (?,?,?,?,?,?,?,?,?,?,?)', [
    (r.id_client, r.addr_state, r.annual_inc, val(r.emp_length), r.home_ownership, val(r.dti),
     val(r.revol_util), val(r.num_bc_tl), val(r.mort_acc), val(r.open_acc), val(r.delinq_2yrs))
    for r in d.itertuples()])
db.executemany("""INSERT INTO Credite(id_client, id_tip, id_stare, suma, suma_finantata, dobanda, durata_luni,
    rata_lunara, grad_risc, data_acordarii, total_platit, principal_rambursat, dobanda_incasata, penalitati,
    recuperari, cost_recuperare, sold_ramas, data_ultimei_plati) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""", [
    (r.id_client, id_tip[r.purpose], id_stare[r.loan_status], r.loan_amnt, r.funded_amnt, r.int_rate,
     r.durata_luni, r.installment, r.grade, r.data_acordarii, r.total_pymnt, r.total_rec_prncp,
     r.total_rec_int, r.total_rec_late_fee, r.recoveries, r.collection_recovery_fee, r.out_prncp,
     r.data_ultimei_plati)
    for r in d.itertuples()])

db.executescript("""
-- Cerinta 1: ponderea fiecarui tip de credit in total
CREATE VIEW v_PondereTipuri AS
SELECT t.denumire AS tip_credit, COUNT(*) AS nr_credite, ROUND(SUM(c.suma), 2) AS total_suma,
       ROUND(100.0 * SUM(c.suma) / (SELECT SUM(suma) FROM Credite), 2) AS procent
FROM Credite c JOIN TipuriCredit t USING (id_tip)
GROUP BY t.denumire ORDER BY total_suma DESC;

-- Cerinta 2: clienti rau-platnici
CREATE VIEW v_RauPlatnici AS
SELECT cl.id_client, cl.stat, cl.venit_anual, t.denumire AS tip_credit, c.suma, c.dobanda, c.grad_risc,
       c.data_acordarii, c.sold_ramas, s.denumire AS stare, s.gravitate
FROM Credite c JOIN Clienti cl USING (id_client) JOIN TipuriCredit t USING (id_tip)
     JOIN StariCredit s USING (id_stare)
WHERE s.rau_platnic = 1;

-- Cerintele 3 si 4: volumul lunar (baza pentru prognoza si simularea dobanzii)
CREATE VIEW v_VolumLunar AS
SELECT substr(data_acordarii, 1, 7) AS luna, COUNT(*) AS nr_credite, ROUND(SUM(suma), 2) AS volum,
       ROUND(AVG(dobanda), 2) AS dobanda_medie
FROM Credite GROUP BY luna ORDER BY luna;

-- Cerinta 5: marja neta pe fiecare credit inchis (rambursat sau pierdut)
-- venit = dobanda + penalitati + recuperari - cost recuperare; pierdere = principal nerecuperat
-- costul finantarii bancii NU e in date -> se scade in aplicatie, ca parametru ales de utilizator
CREATE VIEW v_MarjaCredite AS
SELECT c.id_credit, c.id_client, t.denumire AS tip_credit, c.grad_risc, c.suma_finantata, c.durata_luni,
       c.dobanda, s.denumire AS stare,
       c.dobanda_incasata + c.penalitati + c.recuperari - c.cost_recuperare AS venit,
       CASE WHEN s.rau_platnic = 1 THEN c.suma_finantata - c.principal_rambursat ELSE 0 END AS pierdere,
       c.dobanda_incasata + c.penalitati + c.recuperari - c.cost_recuperare
         - CASE WHEN s.rau_platnic = 1 THEN c.suma_finantata - c.principal_rambursat ELSE 0 END AS marja_neta
FROM Credite c JOIN TipuriCredit t USING (id_tip) JOIN StariCredit s USING (id_stare)
WHERE s.inchis = 1;

CREATE VIEW v_MarjaPeTip AS
SELECT tip_credit, COUNT(*) AS nr_credite, ROUND(SUM(suma_finantata), 2) AS total_finantat,
       ROUND(SUM(venit), 2) AS venit, ROUND(SUM(pierdere), 2) AS pierderi, ROUND(SUM(marja_neta), 2) AS marja_neta,
       ROUND(100.0 * SUM(marja_neta) / SUM(suma_finantata), 2) AS marja_procent
FROM v_MarjaCredite GROUP BY tip_credit ORDER BY marja_procent DESC;

-- Cerinta 5: clienti cu risc scazut si produsul complementar recomandat
CREATE VIEW v_CrossSelling AS
SELECT cl.*, c.grad_risc, t.denumire AS tip_credit, s.denumire AS stare,
       CASE
         WHEN cl.venit_anual >= 100000 AND cl.nr_carduri >= 3 THEN 'Card de credit premium'
         WHEN cl.tip_locuinta = 'MORTGAGE' OR cl.nr_ipoteci > 0  THEN 'Asigurare de viata'
         ELSE 'Cont de economii / depozit'
       END AS produs_recomandat
FROM Credite c JOIN Clienti cl USING (id_client) JOIN TipuriCredit t USING (id_tip)
     JOIN StariCredit s USING (id_stare)
WHERE c.grad_risc IN ('A', 'B') AND s.rau_platnic = 0 AND COALESCE(cl.intarzieri_2ani, 0) = 0;

-- Cerinta 6: rambursari anticipate (credite rambursate integral inainte de termen)
CREATE VIEW v_RambursariAnticipate AS
SELECT * FROM (
  SELECT c.id_credit, cl.id_client, cl.stat, cl.venit_anual, cl.tip_locuinta, cl.vechime_munca,
         t.denumire AS tip_credit, c.grad_risc, c.suma_finantata, c.dobanda, c.durata_luni,
         c.data_acordarii, c.data_ultimei_plati,
         (CAST(strftime('%Y', c.data_ultimei_plati) AS INTEGER) - CAST(strftime('%Y', c.data_acordarii) AS INTEGER)) * 12
         + CAST(strftime('%m', c.data_ultimei_plati) AS INTEGER) - CAST(strftime('%m', c.data_acordarii) AS INTEGER)
           AS luni_efective,
         c.total_platit,
         -- dobanda pierduta ~ ce ar fi platit clientul pe tot graficul minus ce a platit efectiv
         MAX(c.rata_lunara * c.durata_luni - c.total_platit, 0) AS dobanda_pierduta
  FROM Credite c JOIN Clienti cl USING (id_client) JOIN TipuriCredit t USING (id_tip)
       JOIN StariCredit s USING (id_stare)
  WHERE s.inchis = 1 AND s.rau_platnic = 0 AND c.data_ultimei_plati IS NOT NULL
) WHERE luni_efective < durata_luni - 2;   -- cel putin 3 luni mai devreme decat scadenta finala
""")
db.commit()
db.execute('VACUUM')
db.close()
print(f'Baza {destinatie} creata: {len(d)} credite')
