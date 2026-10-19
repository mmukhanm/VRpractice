"""Regression tests use temporary synthetic inputs, never reported as Unity measurements."""
import csv
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('analysis',Path(__file__).with_name('analyze_profiling.py'))
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)

class AnalysisTests(unittest.TestCase):
    def capture(self,directory,count,series='Baseline',complete='1',rows=300):
        path=Path(directory)/f'{series}_{count}.csv'
        fields=['label','stage','checkpoint','N','series','behaviour','physics_case','lighting_case','complete','frame_ms','gpu_ms','awake_bodies']
        with path.open('w',newline='') as f:
            writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader()
            for i in range(rows):writer.writerow(dict(label='SYNTHETIC_TEST_ONLY',stage='3',checkpoint='CP2',N=count,series=series,behaviour='None',physics_case='None_Sleeping',lighting_case='Workshop',complete=complete,frame_ms=6+count*.01,gpu_ms='NaN',awake_bodies=0))
        return analysis.summarize(path)

    def test_missing_gpu_is_not_reported_as_zero(self):
        with tempfile.TemporaryDirectory() as d:
            item=self.capture(d,100)
            self.assertIsNone(item['gpu_ms_mean']);self.assertEqual(item['gpu_ms_valid_frames'],0)

    def test_incomplete_and_short_records_do_not_establish_maximum_n(self):
        with tempfile.TemporaryDirectory() as d:
            items=[self.capture(d,n) for n in analysis.COUNTS[:-1]]
            items.append(self.capture(d,2000,complete='0'))
            results=analysis.graphics_results(items)
            self.assertIsNone(analysis.maximum_counts(results,'Baseline')[0])
            short=self.capture(d,2000,rows=299)
            self.assertFalse(short['complete'])

    def test_complete_series_threshold_and_excel_chart(self):
        with tempfile.TemporaryDirectory() as d:
            items=[self.capture(d,n) for n in analysis.COUNTS]
            limits=analysis.maximum_counts(analysis.graphics_results(items),'Baseline')
            self.assertEqual(limits[:2],(500,500))
            book=analysis.workbook(items)
            self.assertEqual(book['Графика']['B2'].value,6)
            self.assertIsNone(book['Графика']['H2'].value) # No fictitious SRP measurement.
            self.assertEqual(len(book['Графика']._charts[0].series),5)
            self.assertEqual(book['Графика']['Z7'].value,13.9)
            book.save(Path(d)/'SYNTHETIC_TEST_ONLY.xlsx')

    def test_missing_frame_times_cannot_complete_a_series(self):
        with tempfile.TemporaryDirectory() as d:
            item=self.capture(d,100);item['frame_ms_valid_frames']=299
            self.assertEqual(analysis.graphics_results([item]),{})

    def test_over_budget_at_zero_does_not_claim_zero_as_a_passing_n(self):
        with tempfile.TemporaryDirectory() as d:
            items=[self.capture(d,n) for n in analysis.COUNTS]
            for item in items:item['frame_ms_mean']=item['frame_ms_max']=20
            limits=analysis.maximum_counts(analysis.graphics_results(items),'Baseline')
            self.assertEqual(limits[:2],('Нет N','Нет N'))

if __name__=='__main__':unittest.main()
