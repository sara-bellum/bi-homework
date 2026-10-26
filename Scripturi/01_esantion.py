# Pasul 1: extrage un esantion aleator de 4% din loan.csv (Lending Club, 1,2 GB)
# Fisierul e citit pe bucati de 200.000 de randuri ca sa nu umple memoria.
# Rulare: python 01_esantion.py loan.csv lending_club_sample.csv
import sys
import pandas as pd

COLOANE = [
    # date despre credit
    'loan_amnt', 'funded_amnt', 'term', 'int_rate', 'installment', 'grade',
    'issue_d', 'loan_status', 'purpose',
    # date despre client
    'emp_length', 'home_ownership', 'annual_inc', 'addr_state', 'dti',
    'revol_util', 'num_bc_tl', 'mort_acc', 'open_acc', 'delinq_2yrs',
    # plati efective (pentru marja neta si rambursari anticipate)
    'total_pymnt', 'total_rec_prncp', 'total_rec_int', 'total_rec_late_fee',
    'recoveries', 'collection_recovery_fee', 'last_pymnt_d', 'out_prncp',
]

sursa, destinatie = sys.argv[1], sys.argv[2]
bucati = []
for i, bucata in enumerate(pd.read_csv(sursa, usecols=COLOANE, chunksize=200_000, dtype=str)):
    bucati.append(bucata.sample(frac=0.04, random_state=42 + i))  # random_state => rezultat reproductibil
esantion = pd.concat(bucati, ignore_index=True)
esantion.to_csv(destinatie, index=False)
print(f'{len(esantion)} credite salvate in {destinatie}')
